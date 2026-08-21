using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Taskpilot.API.Common;
using Taskpilot.API.Configuration;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Auth;
using Taskpilot.API.Models;

namespace Taskpilot.API.Services;

/// <inheritdoc />
public class DemoService : IDemoService
{
    private readonly TaskpilotDbContext _context;
    private readonly ITokenService _tokens;
    private readonly DemoOptions _options;
    private readonly JwtSettings _jwt;
    private readonly ILogger<DemoService> _logger;

    public DemoService(
        TaskpilotDbContext context,
        ITokenService tokens,
        IOptions<DemoOptions> options,
        IOptions<JwtSettings> jwt,
        ILogger<DemoService> logger)
    {
        _context = context;
        _tokens = tokens;
        _options = options.Value;
        _jwt = jwt.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool IsEnabled => _options.Enabled;

    /// <inheritdoc />
    public async Task<Result<AuthResponseDto>> CreateDemoAsync(string? ip, string? userAgent)
    {
        if (!_options.Enabled)
            return Result<AuthResponseDto>.Fail("The demo is not available.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = "Demo User",
            // Unique, obviously-throwaway address. Never used to sign in (there's no password login).
            Email = $"demo-{Guid.NewGuid():N}@demo.taskpilot.local",
            // A random hash so the row is well-formed but the account is not password-loggable.
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString("N")),
            Role = Role.Developer,
            IsActive = true,
            IsDemo = true,
            CreatedAt = DateTime.UtcNow,
        };
        _context.Users.Add(user);

        SeedSampleData(user.Id);

        var (accessToken, accessExpiresAtUtc) = _tokens.GenerateAccessToken(user);
        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            Token = _tokens.GenerateRefreshToken(),
            UserId = user.Id,
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(_jwt.RefreshTokenDays),
            IpAddress = ip,
            UserAgent = userAgent is { Length: > 400 } ua ? ua[..400] : userAgent,
        };
        _context.RefreshTokens.Add(refreshToken);

        await _context.SaveChangesAsync();
        _logger.LogInformation("Demo account created. UserId: {UserId}", user.Id);

        return Result<AuthResponseDto>.Ok(new AuthResponseDto
        {
            AccessToken = accessToken,
            ExpiresAtUtc = accessExpiresAtUtc,
            RefreshToken = refreshToken.Token,
            RefreshTokenExpiresAtUtc = refreshToken.ExpiresAtUtc,
            UserId = user.Id,
            Email = user.Email,
            Role = user.Role.ToString(),
        });
    }

    /// <inheritdoc />
    public async Task<int> PurgeExpiredAsync()
    {
        if (_options.RetentionHours <= 0) return 0;
        var cutoff = DateTime.UtcNow.AddHours(-_options.RetentionHours);

        var expired = await _context.Users
            .Where(u => u.IsDemo && u.CreatedAt < cutoff)
            .Select(u => u.Id)
            .ToListAsync();
        if (expired.Count == 0) return 0;

        foreach (var userId in expired)
        {
            // Delete the visible sandbox — the demo's own projects cascade to their tasks/members/etc.
            var projects = await _context.Projects.Where(p => p.OwnerId == userId).ToListAsync();
            _context.Projects.RemoveRange(projects);

            // Personal records tied only to the demo account.
            _context.Notes.RemoveRange(_context.Notes.Where(n => n.OwnerId == userId));
            _context.Bookmarks.RemoveRange(_context.Bookmarks.Where(b => b.UserId == userId));
            _context.SavedSearches.RemoveRange(_context.SavedSearches.Where(s => s.UserId == userId));
            _context.ProjectMembers.RemoveRange(_context.ProjectMembers.Where(m => m.UserId == userId));
            _context.RefreshTokens.RemoveRange(_context.RefreshTokens.Where(rt => rt.UserId == userId));

            // Soft-retire the user row itself: hard-deleting a User fights the many Restrict FKs
            // (forum/chat/comments a demo visitor may have created), so anonymize + deactivate and
            // clear IsDemo so a later run skips it. The sandbox above is already gone.
            var user = await _context.Users.FirstAsync(u => u.Id == userId);
            user.Name = "Former demo user";
            user.Email = $"demo-expired-{userId:N}@deleted.local";
            user.PasswordHash = null;
            user.IsActive = false;
            user.IsDemo = false;
            user.TelegramChatId = null;
            user.ViberId = null;
            user.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Purged {Count} expired demo account(s).", expired.Count);
        return expired.Count;
    }

    /// <summary>Seeds a realistic sample project so the demo isn't an empty shell on first load.</summary>
    private void SeedSampleData(Guid userId)
    {
        var projectId = Guid.NewGuid();
        _context.Projects.Add(new Project
        {
            Id = projectId,
            Name = "Product Launch",
            Description = "A sample project to explore boards, tasks, planning and more.",
            Color = "#6366f1",
            OwnerId = userId,
            CreatedAt = DateTime.UtcNow,
        });

        var now = DateTime.UtcNow;
        void Task(string title, ProjectTaskStatus status, TaskPriority priority, DateTime? deadline, string? desc = null)
        {
            _context.ProjectTasks.Add(new ProjectTask
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Title = title,
                Description = desc,
                Status = status,
                Priority = priority,
                AssigneeId = userId,
                CreatorId = userId,
                Deadline = deadline,
                CreatedAt = now,
            });
        }

        Task("Define launch scope", ProjectTaskStatus.Done, TaskPriority.High, now.AddDays(-6));
        Task("Design landing page", ProjectTaskStatus.Done, TaskPriority.Medium, now.AddDays(-2));
        Task("Write announcement post", ProjectTaskStatus.InProgress, TaskPriority.High, now.AddDays(-1),
            "Overdue — a good candidate for the \"What to do next\" planner.");
        Task("Set up analytics", ProjectTaskStatus.InProgress, TaskPriority.Medium, now.AddDays(2));
        Task("QA the checkout flow", ProjectTaskStatus.Review, TaskPriority.High, now.AddDays(1));
        Task("Prepare press kit", ProjectTaskStatus.Backlog, TaskPriority.Low, now.AddDays(5));
        Task("Plan launch-day schedule", ProjectTaskStatus.Backlog, TaskPriority.Medium, null);

        _context.Notes.Add(new Note
        {
            Id = Guid.NewGuid(),
            OwnerId = userId,
            Title = "Welcome to TaskPilot 👋",
            Content = "This is a demo account with sample data. Explore the board, drag tasks between "
                    + "columns, open the calendar and timeline, or ask the AI assistant. Everything here "
                    + "is yours to play with — it resets automatically.",
            Color = "#f59e0b",
            IsPinned = true,
            CreatedAt = now,
        });
    }
}
