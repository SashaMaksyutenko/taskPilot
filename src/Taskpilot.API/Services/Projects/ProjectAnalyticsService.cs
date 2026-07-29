using Microsoft.EntityFrameworkCore;
using Taskpilot.API.Common;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Projects;
using Taskpilot.API.Models;

namespace Taskpilot.API.Services;

/// <inheritdoc />
public class ProjectAnalyticsService : IProjectAnalyticsService
{
    private const int WeeksWindow = 8;

    private readonly TaskpilotDbContext _context;

    public ProjectAnalyticsService(TaskpilotDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<Result<ProjectAnalyticsDto>> GetAnalyticsAsync(Guid userId, Guid projectId)
    {
        if (!await ProjectAccess.CanAccessAsync(_context, projectId, userId))
            return Result<ProjectAnalyticsDto>.Fail("Project not found.");

        var tasks = await _context.ProjectTasks
            .Where(t => t.ProjectId == projectId)
            .Select(t => new
            {
                t.Status, t.Priority, t.CreatedAt, t.CompletedAt,
                AssigneeName = t.Assignee != null ? t.Assignee.Name : null,
            })
            .AsNoTracking()
            .ToListAsync();

        // Status/priority mix — every column present even at 0.
        var byStatus = Enum.GetValues<ProjectTaskStatus>().ToDictionary(s => s.ToString(), _ => 0);
        var byPriority = Enum.GetValues<TaskPriority>().ToDictionary(p => p.ToString(), _ => 0);
        foreach (var task in tasks)
        {
            byStatus[task.Status.ToString()]++;
            byPriority[task.Priority.ToString()]++;
        }

        // Weekly created/completed trend over the last 8 weeks (Monday-based, UTC).
        var currentMonday = MondayOf(DateTime.UtcNow);
        var weeks = new List<WeekBucketDto>();
        for (var start = currentMonday.AddDays(-7 * (WeeksWindow - 1)); start <= currentMonday; start = start.AddDays(7))
        {
            var end = start.AddDays(7);
            weeks.Add(new WeekBucketDto
            {
                WeekStart = start,
                Created = tasks.Count(t => t.CreatedAt >= start && t.CreatedAt < end),
                Completed = tasks.Count(t => t.CompletedAt is { } c && c >= start && c < end),
            });
        }

        // Cycle time: average days from creation to completion for finished tasks.
        var cycleDays = tasks
            .Where(t => t.CompletedAt is not null)
            .Select(t => (t.CompletedAt!.Value - t.CreatedAt).TotalDays)
            .ToList();

        var byAssignee = tasks
            .GroupBy(t => t.AssigneeName ?? "Unassigned")
            .Select(g => new AssigneeLoadDto
            {
                Name = g.Key,
                Open = g.Count(t => t.Status != ProjectTaskStatus.Done),
                Done = g.Count(t => t.Status == ProjectTaskStatus.Done),
            })
            .OrderByDescending(a => a.Open + a.Done)
            .ToList();

        return Result<ProjectAnalyticsDto>.Ok(new ProjectAnalyticsDto
        {
            TotalTasks = tasks.Count,
            ByStatus = byStatus,
            ByPriority = byPriority,
            Weeks = weeks,
            AvgCycleTimeDays = cycleDays.Count > 0 ? Math.Round(cycleDays.Average(), 1) : null,
            ThroughputThisWeek = weeks[^1].Completed,
            ThroughputPrevWeek = weeks.Count > 1 ? weeks[^2].Completed : 0,
            ByAssignee = byAssignee,
        });
    }

    /// <summary>Midnight (UTC) of the Monday of the given date's week.</summary>
    private static DateTime MondayOf(DateTime date)
    {
        var offset = ((int)date.DayOfWeek + 6) % 7; // Sun=0 → 6, Mon=1 → 0, …
        return date.Date.AddDays(-offset);
    }
}
