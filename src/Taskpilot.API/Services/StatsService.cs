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
/// Computes live site statistics. User/forum counts come from the database; online
/// users from <see cref="PresenceTracker"/> (SignalR connections); anonymous visitors
/// from <see cref="IVisitorService"/> (persisted). The heavy aggregate figures are
/// cached briefly (Redis when configured) to avoid running a batch of COUNT queries on
/// every dashboard refresh, while presence and visitor counts stay live.
/// </summary>
public class StatsService : IStatsService
{
    private const string FullStatsCacheKey = "stats:full";
    private static readonly TimeSpan FullStatsTtl = TimeSpan.FromSeconds(15);

    private readonly TaskpilotDbContext _context;
    private readonly PresenceTracker _presence;
    private readonly IVisitorService _visitors;
    private readonly IDistributedCache _cache;

    public StatsService(TaskpilotDbContext context, PresenceTracker presence, IVisitorService visitors, IDistributedCache cache)
    {
        _context = context;
        _presence = presence;
        _visitors = visitors;
        _cache = cache;
    }

    /// <inheritdoc />
    public async Task<Result<AdminStatsDto>> GetFullStatsAsync()
    {
        // Heavy aggregates (user/role/forum counts) are cached; presence and visitor
        // counts are always live so a just-connected user shows up immediately.
        var agg = await GetAggregatesAsync();
        var online = await OnlineNamesAsync();

        return Result<AdminStatsDto>.Ok(new AdminStatsDto
        {
            UsersByRole = agg.UsersByRole,
            TotalUsers = agg.TotalUsers,
            ActiveUsers = agg.ActiveUsers,
            NewestUserName = agg.NewestUserName,
            TotalTopics = agg.TotalTopics,
            TotalForumPosts = agg.TotalForumPosts,
            OnlineUsers = online.Count,
            OnlineUserNames = online,
            // Anonymous-visitor analytics are admin-only.
            AnonymousVisitorsToday = await _visitors.UniqueVisitorsTodayAsync(),
            AnonymousVisitsTotal = await _visitors.TotalVisitsAsync(),
        });
    }

    /// <inheritdoc />
    public async Task<Result<PublicStatsDto>> GetPublicStatsAsync()
    {
        var common = await LoadCommonAsync();
        var online = await OnlineNamesAsync();

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

    // Cache-aside for the expensive aggregates only (never the live presence count).
    private async Task<StatsAggregates> GetAggregatesAsync()
    {
        var cached = await _cache.GetStringAsync(FullStatsCacheKey);
        if (cached is not null)
            return JsonSerializer.Deserialize<StatsAggregates>(cached)!;

        var common = await LoadCommonAsync();

        // Count users grouped by role for the breakdown chart (role stored as string).
        var byRole = await _context.Users
            .GroupBy(u => u.Role)
            .Select(g => new { Role = g.Key, Count = g.Count() })
            .ToListAsync();

        var agg = new StatsAggregates(
            byRole.ToDictionary(x => x.Role.ToString(), x => x.Count),
            common.TotalUsers, common.ActiveUsers, common.NewestUserName, common.TotalTopics, common.TotalForumPosts);

        await _cache.SetStringAsync(FullStatsCacheKey, JsonSerializer.Serialize(agg),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = FullStatsTtl });

        return agg;
    }

    // Names of users currently online — always computed live from the presence tracker.
    private async Task<List<string>> OnlineNamesAsync()
    {
        var onlineIds = _presence.OnlineUserIds();
        if (onlineIds.Count == 0)
            return new List<string>();

        return await _context.Users
            .Where(u => onlineIds.Contains(u.Id))
            .OrderBy(u => u.Name)
            .Select(u => u.Name)
            .ToListAsync();
    }

    // Expensive counts shared by both stat views (cached for admin, fresh for public).
    private async Task<CommonStats> LoadCommonAsync()
    {
        var totalUsers = await _context.Users.CountAsync();
        var activeUsers = await _context.Users.CountAsync(u => u.IsActive);

        var newestUserName = await _context.Users
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => u.Name)
            .FirstOrDefaultAsync();

        var totalTopics = await _context.ForumTopics.CountAsync();
        var totalForumPosts = await _context.ForumReplies.CountAsync();

        return new CommonStats(totalUsers, activeUsers, newestUserName, totalTopics, totalForumPosts);
    }

    // Small carrier for the counts shared by both stat views.
    private readonly record struct CommonStats(
        int TotalUsers, int ActiveUsers, string? NewestUserName, int TotalTopics, int TotalForumPosts);

    // Cacheable aggregate snapshot (excludes live presence/visitor counts).
    private sealed record StatsAggregates(
        Dictionary<string, int> UsersByRole, int TotalUsers, int ActiveUsers,
        string? NewestUserName, int TotalTopics, int TotalForumPosts);
}
