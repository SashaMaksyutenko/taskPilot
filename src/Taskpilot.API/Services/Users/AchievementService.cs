using Microsoft.EntityFrameworkCore;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Users;
using Taskpilot.API.Models;

namespace Taskpilot.API.Services;

/// <inheritdoc />
public class AchievementService : IAchievementService
{
    private readonly TaskpilotDbContext _context;

    public AchievementService(TaskpilotDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<List<AchievementDto>> GetForUserAsync(Guid userId)
    {
        var tasksDone = await _context.ProjectTasks
            .CountAsync(t => t.AssigneeId == userId && t.Status == ProjectTaskStatus.Done);

        var score = await _context.ReputationEntries
            .Where(e => e.UserId == userId)
            .SumAsync(e => (int?)e.Delta) ?? 0;

        var onTime = await _context.ReputationEntries
            .CountAsync(e => e.UserId == userId &&
                (e.Reason == ReputationReason.TaskEarly || e.Reason == ReputationReason.TaskOnTime));

        var solutions = await _context.ReputationEntries
            .CountAsync(e => e.UserId == userId && e.Reason == ReputationReason.ForumSolution);

        var market = await _context.ReputationEntries
            .CountAsync(e => e.UserId == userId && e.Reason == ReputationReason.MarketplaceCompleted);

        static AchievementDto Badge(string code, int current, int target) => new()
        {
            Code = code,
            Current = current,
            Target = target,
            Earned = current >= target,
        };

        // Fixed badge set — thresholds are deliberately modest so early activity is rewarded.
        return new List<AchievementDto>
        {
            Badge("first_task", tasksDone, 1),
            Badge("ten_tasks", tasksDone, 10),
            Badge("fifty_tasks", tasksDone, 50),
            Badge("punctual", onTime, 5),
            Badge("reputable", Math.Max(score, 0), 100),
            Badge("solver", solutions, 1),
            Badge("trader", market, 1),
        };
    }
}
