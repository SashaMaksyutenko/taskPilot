using Microsoft.EntityFrameworkCore;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Users;
using Taskpilot.API.Mappers;
using Taskpilot.API.Models;

namespace Taskpilot.API.Services;

/// <inheritdoc />
public class LeaderboardService : ILeaderboardService
{
    private readonly TaskpilotDbContext _context;

    public LeaderboardService(TaskpilotDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<LeaderboardDto> GetAsync(Guid currentUserId, int limit = 20)
    {
        if (limit is < 1 or > 100) limit = 20;

        // Reputation score per user (positive contributors only).
        var scores = await _context.ReputationEntries
            .GroupBy(e => e.UserId)
            .Select(g => new { UserId = g.Key, Score = g.Sum(e => e.Delta) })
            .Where(x => x.Score > 0)
            .AsNoTracking()
            .ToListAsync();

        // Completed tasks per user (used as a stat and a tie-breaker).
        var completed = await _context.ProjectTasks
            .Where(t => t.Status == ProjectTaskStatus.Done && t.AssigneeId != null)
            .GroupBy(t => t.AssigneeId!.Value)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .AsNoTracking()
            .ToListAsync();
        var completedByUser = completed.ToDictionary(x => x.UserId, x => x.Count);

        // Rank: score desc, then completed desc, then id for a stable order.
        var ranked = scores
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => completedByUser.GetValueOrDefault(x.UserId))
            .ThenBy(x => x.UserId)
            .Select((x, i) => new { x.UserId, x.Score, Rank = i + 1 })
            .ToList();

        // Load display info for the users we'll actually return (top N + the caller).
        var neededIds = ranked.Take(limit).Select(r => r.UserId).ToHashSet();
        var meRanked = ranked.FirstOrDefault(r => r.UserId == currentUserId);
        if (meRanked is not null) neededIds.Add(currentUserId);

        var users = await _context.Users
            .Where(u => neededIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Name, u.AvatarFileId })
            .AsNoTracking()
            .ToListAsync();
        var userById = users.ToDictionary(u => u.Id);

        LeaderboardEntryDto? Map(Guid userId, int score, int rank)
        {
            if (!userById.TryGetValue(userId, out var u)) return null;
            return new LeaderboardEntryDto
            {
                Rank = rank,
                UserId = userId,
                Name = u.Name,
                AvatarUrl = UserMapper.AvatarUrl(userId, u.AvatarFileId),
                Score = score,
                TasksCompleted = completedByUser.GetValueOrDefault(userId),
            };
        }

        var entries = ranked
            .Take(limit)
            .Select(r => Map(r.UserId, r.Score, r.Rank))
            .OfType<LeaderboardEntryDto>()
            .ToList();

        var me = meRanked is null ? null : Map(meRanked.UserId, meRanked.Score, meRanked.Rank);

        return new LeaderboardDto { Entries = entries, Me = me };
    }
}
