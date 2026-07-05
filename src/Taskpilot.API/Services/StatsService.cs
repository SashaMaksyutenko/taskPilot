using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Taskpilot.API.Common;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Admin;
using Taskpilot.API.DTOs.Stats;
using Taskpilot.API.Hubs;

namespace Taskpilot.API.Services;

/// <summary>
/// Computes live site statistics. User/forum counts come from the database;
/// online users from <see cref="PresenceTracker"/> (SignalR connections) and
/// anonymous visitors from <see cref="VisitorTracker"/> (both in-memory singletons).
/// The admin dashboard figures are cached briefly (Redis when configured) to avoid
/// running a batch of COUNT queries on every dashboard refresh.
/// </summary>
public class StatsService : IStatsService
{
    private const string FullStatsCacheKey = "stats:full";
    private static readonly TimeSpan FullStatsTtl = TimeSpan.FromSeconds(15);

    private readonly TaskpilotDbContext _context;
    private readonly PresenceTracker _presence;
    private readonly VisitorTracker _visitors;
    private readonly IDistributedCache _cache;

    public StatsService(TaskpilotDbContext context, PresenceTracker presence, VisitorTracker visitors, IDistributedCache cache)
    {
        _context = context;
        _presence = presence;
        _visitors = visitors;
        _cache = cache;
    }

    /// <inheritdoc />
    public async Task<Result<AdminStatsDto>> GetFullStatsAsync()
    {
        // Cache-aside: serve a recent snapshot when available.
        var cached = await _cache.GetStringAsync(FullStatsCacheKey);
        if (cached is not null)
            return Result<AdminStatsDto>.Ok(JsonSerializer.Deserialize<AdminStatsDto>(cached)!);

        var (common, online) = await LoadCommonAsync();

        // Count users grouped by role for the breakdown chart (role stored as string).
        var byRole = await _context.Users
            .GroupBy(u => u.Role)
            .Select(g => new { Role = g.Key, Count = g.Count() })
            .ToListAsync();

        var dto = new AdminStatsDto
        {
            UsersByRole = byRole.ToDictionary(x => x.Role.ToString(), x => x.Count),
            TotalUsers = common.TotalUsers,
            ActiveUsers = common.ActiveUsers,
            NewestUserName = common.NewestUserName,
            TotalTopics = common.TotalTopics,
            TotalForumPosts = common.TotalForumPosts,
            OnlineUsers = online.Count,
            OnlineUserNames = online,
            // Anonymous-visitor analytics are admin-only.
            AnonymousVisitorsToday = _visitors.UniqueVisitorsToday,
            AnonymousVisitsTotal = _visitors.TotalVisits,
        };

        await _cache.SetStringAsync(FullStatsCacheKey, JsonSerializer.Serialize(dto),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = FullStatsTtl });

        return Result<AdminStatsDto>.Ok(dto);
    }

    /// <inheritdoc />
    public async Task<Result<PublicStatsDto>> GetPublicStatsAsync()
    {
        var (common, online) = await LoadCommonAsync();

        return Result<PublicStatsDto>.Ok(new PublicStatsDto
        {
            TotalUsers = common.TotalUsers,
            NewestUserName = common.NewestUserName,
            TotalTopics = common.TotalTopics,
            TotalForumPosts = common.TotalForumPosts,
            OnlineUsers = online.Count,
            OnlineUserNames = online,
        });
    }

    // Shared data the two stat views have in common, plus the online user names.
    private async Task<(CommonStats Stats, List<string> OnlineNames)> LoadCommonAsync()
    {
        var totalUsers = await _context.Users.CountAsync();
        var activeUsers = await _context.Users.CountAsync(u => u.IsActive);

        // Newest registered user's name (for the "new user" line).
        var newestUserName = await _context.Users
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => u.Name)
            .FirstOrDefaultAsync();

        var totalTopics = await _context.ForumTopics.CountAsync();
        var totalForumPosts = await _context.ForumReplies.CountAsync();

        // Resolve the names of the users currently online (registered, real-time).
        var onlineIds = _presence.OnlineUserIds();
        var onlineNames = onlineIds.Count == 0
            ? new List<string>()
            : await _context.Users
                .Where(u => onlineIds.Contains(u.Id))
                .OrderBy(u => u.Name)
                .Select(u => u.Name)
                .ToListAsync();

        return (new CommonStats(totalUsers, activeUsers, newestUserName, totalTopics, totalForumPosts), onlineNames);
    }

    // Small carrier for the counts shared by both stat views.
    private readonly record struct CommonStats(
        int TotalUsers, int ActiveUsers, string? NewestUserName, int TotalTopics, int TotalForumPosts);
}
