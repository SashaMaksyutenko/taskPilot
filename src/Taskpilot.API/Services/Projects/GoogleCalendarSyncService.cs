using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Taskpilot.API.Common;
using Taskpilot.API.Configuration;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Calendar;
using Taskpilot.API.Models;

namespace Taskpilot.API.Services;

/// <summary>
/// Pushes a user's deadline tasks to their own Google Calendar as real (editable, reminder-
/// capable) events — more than the read-only iCal feed offers. Stores a per-task event link so
/// re-syncs update the same event instead of duplicating it. Access tokens are refreshed on
/// demand from the stored refresh token.
/// </summary>
public class GoogleCalendarSyncService : IGoogleCalendarSyncService
{
    // Manage events on the user's own calendars (not read their whole account).
    private const string Scope = "https://www.googleapis.com/auth/calendar.events";

    private readonly TaskpilotDbContext _context;
    private readonly IGoogleCalendarClient _client;
    private readonly ITaskService _tasks;
    private readonly GoogleOAuthOptions _options;
    private readonly ILogger<GoogleCalendarSyncService> _logger;

    public GoogleCalendarSyncService(
        TaskpilotDbContext context,
        IGoogleCalendarClient client,
        ITaskService tasks,
        IOptions<GoogleOAuthOptions> options,
        ILogger<GoogleCalendarSyncService> logger)
    {
        _context = context;
        _client = client;
        _tasks = tasks;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<GoogleCalendarStatusDto> GetStatusAsync(Guid userId)
    {
        var conn = await _context.GoogleCalendarConnections.AsNoTracking()
            .FirstOrDefaultAsync(c => c.UserId == userId);
        return new GoogleCalendarStatusDto
        {
            Configured = _client.IsEnabled,
            Connected = conn is not null,
            LastSyncedUtc = conn?.LastSyncedUtc,
        };
    }

    /// <inheritdoc />
    public string BuildConnectUrl(string redirectUri, string state)
    {
        // access_type=offline + prompt=consent guarantees a refresh token on connect.
        var query = new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId,
            ["redirect_uri"] = redirectUri,
            ["response_type"] = "code",
            ["scope"] = Scope,
            ["access_type"] = "offline",
            ["prompt"] = "consent",
            ["state"] = state,
        };
        var qs = string.Join("&", query.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
        return $"https://accounts.google.com/o/oauth2/v2/auth?{qs}";
    }

    /// <inheritdoc />
    public async Task<Result> ConnectAsync(Guid userId, string code, string redirectUri)
    {
        if (!_client.IsEnabled) return Result.Fail("Google Calendar sync is not configured.");
        if (string.IsNullOrWhiteSpace(code)) return Result.Fail("Missing authorization code.");

        var tokens = await _client.ExchangeCodeAsync(code, redirectUri);
        if (!tokens.Succeeded) return Result.Fail(tokens.Error!);
        if (string.IsNullOrEmpty(tokens.Value!.RefreshToken))
            return Result.Fail("Google did not return a refresh token. Remove TaskPilot from your Google account's connected apps, then reconnect.");

        var conn = await _context.GoogleCalendarConnections.FirstOrDefaultAsync(c => c.UserId == userId);
        if (conn is null)
        {
            conn = new GoogleCalendarConnection { UserId = userId };
            _context.GoogleCalendarConnections.Add(conn);
        }
        conn.RefreshToken = tokens.Value.RefreshToken!;
        conn.AccessToken = tokens.Value.AccessToken;
        conn.AccessTokenExpiresUtc = DateTime.UtcNow.AddSeconds(tokens.Value.ExpiresInSeconds);
        await _context.SaveChangesAsync();
        return Result.Ok();
    }

    /// <inheritdoc />
    public async Task<Result> DisconnectAsync(Guid userId)
    {
        var conn = await _context.GoogleCalendarConnections.FirstOrDefaultAsync(c => c.UserId == userId);
        if (conn is not null) _context.GoogleCalendarConnections.Remove(conn);

        var links = await _context.GoogleCalendarEventLinks.Where(l => l.UserId == userId).ToListAsync();
        if (links.Count > 0) _context.GoogleCalendarEventLinks.RemoveRange(links);

        await _context.SaveChangesAsync();
        return Result.Ok();
    }

    /// <inheritdoc />
    public async Task<Result<GoogleCalendarSyncResultDto>> SyncAsync(Guid userId)
    {
        if (!_client.IsEnabled) return Result<GoogleCalendarSyncResultDto>.Fail("Google Calendar sync is not configured.");
        var conn = await _context.GoogleCalendarConnections.FirstOrDefaultAsync(c => c.UserId == userId);
        if (conn is null) return Result<GoogleCalendarSyncResultDto>.Fail("Google Calendar is not connected.");

        var tokenResult = await EnsureAccessTokenAsync(conn);
        if (!tokenResult.Succeeded) return Result<GoogleCalendarSyncResultDto>.Fail(tokenResult.Error!);
        var accessToken = tokenResult.Value!;

        // The tasks to mirror: the user's deadline tasks ±1 year (same window as the iCal feed).
        var from = DateTime.UtcNow.AddYears(-1);
        var to = DateTime.UtcNow.AddYears(1);
        var tasksResult = await _tasks.GetCalendarTasksAsync(userId, from, to);
        var calendarTasks = tasksResult.Value ?? new();

        var links = await _context.GoogleCalendarEventLinks.Where(l => l.UserId == userId).ToListAsync();
        var linkByTask = links.ToDictionary(l => l.TaskId);

        int created = 0, updated = 0;
        foreach (var task in calendarTasks)
        {
            var start = task.Deadline.ToUniversalTime();
            var ev = new GoogleCalendarEventData(
                task.Id,
                task.Title,
                $"TaskPilot • {task.ProjectName} • {task.Status}/{task.Priority}",
                start,
                start.AddMinutes(30));

            linkByTask.TryGetValue(task.Id, out var link);
            var upsert = await _client.UpsertEventAsync(accessToken, link?.GoogleEventId, ev);
            if (!upsert.Succeeded)
            {
                // One bad event never aborts the whole sync.
                _logger.LogWarning("Skipping task {TaskId} during Google Calendar sync: {Error}", task.Id, upsert.Error);
                continue;
            }

            if (link is null)
            {
                _context.GoogleCalendarEventLinks.Add(new GoogleCalendarEventLink
                {
                    UserId = userId,
                    TaskId = task.Id,
                    GoogleEventId = upsert.Value!,
                });
                created++;
            }
            else
            {
                link.GoogleEventId = upsert.Value!;
                link.UpdatedAt = DateTime.UtcNow;
                updated++;
            }
        }

        conn.LastSyncedUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Result<GoogleCalendarSyncResultDto>.Ok(new GoogleCalendarSyncResultDto
        {
            Created = created,
            Updated = updated,
            Total = calendarTasks.Count,
        });
    }

    /// <inheritdoc />
    public async Task<Result<GoogleCalendarPullResultDto>> PullAsync(Guid userId)
    {
        if (!_client.IsEnabled) return Result<GoogleCalendarPullResultDto>.Fail("Google Calendar sync is not configured.");
        var conn = await _context.GoogleCalendarConnections.FirstOrDefaultAsync(c => c.UserId == userId);
        if (conn is null) return Result<GoogleCalendarPullResultDto>.Fail("Google Calendar is not connected.");

        var tokenResult = await EnsureAccessTokenAsync(conn);
        if (!tokenResult.Succeeded) return Result<GoogleCalendarPullResultDto>.Fail(tokenResult.Error!);

        var from = DateTime.UtcNow.AddYears(-1);
        var to = DateTime.UtcNow.AddYears(1);
        var eventsResult = await _client.ListEventsAsync(tokenResult.Value!, from, to);
        if (!eventsResult.Succeeded) return Result<GoogleCalendarPullResultDto>.Fail(eventsResult.Error!);

        // Only act on events linked to one of THIS user's tasks (i.e. events we pushed).
        var linkedTaskIds = (await _context.GoogleCalendarEventLinks
            .Where(l => l.UserId == userId).Select(l => l.TaskId).ToListAsync()).ToHashSet();

        int rescheduled = 0, checkedCount = 0;
        foreach (var ev in eventsResult.Value!)
        {
            if (ev.TaskId is null || !linkedTaskIds.Contains(ev.TaskId.Value)) continue;
            checkedCount++;

            var deadline = await _context.ProjectTasks.AsNoTracking()
                .Where(t => t.Id == ev.TaskId.Value)
                .Select(t => t.Deadline)
                .FirstOrDefaultAsync();
            if (deadline is null) continue; // task gone or has no deadline

            // Skip when unchanged (within a minute) — avoids churn and a push/pull feedback loop.
            if (Math.Abs((ev.StartUtc - deadline.Value).TotalSeconds) < 60) continue;

            // Reschedule enforces the user's write access; a refusal is skipped, not fatal.
            var res = await _tasks.RescheduleAsync(userId, ev.TaskId.Value, ev.StartUtc);
            if (res.Succeeded) rescheduled++;
        }

        conn.LastSyncedUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Result<GoogleCalendarPullResultDto>.Ok(new GoogleCalendarPullResultDto
        {
            Rescheduled = rescheduled,
            Checked = checkedCount,
        });
    }

    /// <summary>Returns a valid access token, refreshing (and caching) it when missing or near expiry.</summary>
    private async Task<Result<string>> EnsureAccessTokenAsync(GoogleCalendarConnection conn)
    {
        if (!string.IsNullOrEmpty(conn.AccessToken)
            && conn.AccessTokenExpiresUtc is { } exp
            && exp > DateTime.UtcNow.AddMinutes(1))
        {
            return Result<string>.Ok(conn.AccessToken!);
        }

        var refreshed = await _client.RefreshAccessTokenAsync(conn.RefreshToken);
        if (!refreshed.Succeeded) return Result<string>.Fail(refreshed.Error!);

        conn.AccessToken = refreshed.Value!.AccessToken;
        conn.AccessTokenExpiresUtc = DateTime.UtcNow.AddSeconds(refreshed.Value.ExpiresInSeconds);
        // Persisted by the caller's SaveChanges.
        return Result<string>.Ok(conn.AccessToken!);
    }
}
