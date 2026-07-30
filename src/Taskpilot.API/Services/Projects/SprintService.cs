using Microsoft.EntityFrameworkCore;
using Taskpilot.API.Common;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Projects;
using Taskpilot.API.Models;

namespace Taskpilot.API.Services;

/// <inheritdoc />
public class SprintService : ISprintService
{
    private readonly TaskpilotDbContext _context;
    private readonly ILogger<SprintService> _logger;

    public SprintService(TaskpilotDbContext context, ILogger<SprintService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<List<SprintDto>>> GetSprintsAsync(Guid userId, Guid projectId)
    {
        if (!await ProjectAccess.CanAccessAsync(_context, projectId, userId))
            return Result<List<SprintDto>>.Fail("Project not found.");

        var sprints = await _context.Sprints
            .Where(s => s.ProjectId == projectId)
            .OrderBy(s => s.CreatedAt)
            .ToListAsync();

        // Task tallies per sprint in one query.
        var counts = (await _context.ProjectTasks
                .Where(t => t.ProjectId == projectId && t.SprintId != null)
                .GroupBy(t => t.SprintId!.Value)
                .Select(g => new { SprintId = g.Key, Total = g.Count(), Done = g.Count(t => t.Status == ProjectTaskStatus.Done) })
                .ToListAsync())
            .ToDictionary(c => c.SprintId);

        return Result<List<SprintDto>>.Ok(sprints.Select(s =>
        {
            counts.TryGetValue(s.Id, out var c);
            return MapDto(s, c?.Total ?? 0, c?.Done ?? 0);
        }).ToList());
    }

    /// <inheritdoc />
    public async Task<Result<SprintDto>> CreateSprintAsync(Guid userId, Guid projectId, SaveSprintDto dto)
    {
        if (!await ProjectAccess.CanAccessAsync(_context, projectId, userId))
            return Result<SprintDto>.Fail("Project not found.");
        if (!await ProjectAccess.CanWriteAsync(_context, projectId, userId))
            return Result<SprintDto>.Fail("You have read-only access to this project.");
        if (string.IsNullOrWhiteSpace(dto.Name))
            return Result<SprintDto>.Fail("Sprint name is required.");

        var sprint = new Sprint
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Name = dto.Name.Trim(),
            Goal = string.IsNullOrWhiteSpace(dto.Goal) ? null : dto.Goal.Trim(),
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            Status = SprintStatus.Planned,
            CreatedAt = DateTime.UtcNow,
        };
        _context.Sprints.Add(sprint);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Sprint created. SprintId: {SprintId}, ProjectId: {ProjectId}", sprint.Id, projectId);
        return Result<SprintDto>.Ok(MapDto(sprint, 0, 0));
    }

    /// <inheritdoc />
    public async Task<Result<SprintDto>> UpdateSprintAsync(Guid userId, Guid sprintId, SaveSprintDto dto)
    {
        var sprint = await _context.Sprints.FirstOrDefaultAsync(s => s.Id == sprintId);
        if (sprint is null)
            return Result<SprintDto>.Fail("Sprint not found.");
        if (!await ProjectAccess.CanWriteAsync(_context, sprint.ProjectId, userId))
            return Result<SprintDto>.Fail("You have read-only access to this project.");
        if (string.IsNullOrWhiteSpace(dto.Name))
            return Result<SprintDto>.Fail("Sprint name is required.");
        if (dto.Status is not null && ParseStatus(dto.Status) is not { } status)
            return Result<SprintDto>.Fail("Invalid status. Use Planned, Active or Completed.");

        sprint.Name = dto.Name.Trim();
        sprint.Goal = string.IsNullOrWhiteSpace(dto.Goal) ? null : dto.Goal.Trim();
        sprint.StartDate = dto.StartDate;
        sprint.EndDate = dto.EndDate;
        if (dto.Status is not null)
            sprint.Status = ParseStatus(dto.Status)!.Value;
        await _context.SaveChangesAsync();

        var total = await _context.ProjectTasks.CountAsync(t => t.SprintId == sprintId);
        var done = await _context.ProjectTasks.CountAsync(t => t.SprintId == sprintId && t.Status == ProjectTaskStatus.Done);
        return Result<SprintDto>.Ok(MapDto(sprint, total, done));
    }

    /// <inheritdoc />
    public async Task<Result> DeleteSprintAsync(Guid userId, Guid sprintId)
    {
        var sprint = await _context.Sprints.FirstOrDefaultAsync(s => s.Id == sprintId);
        if (sprint is null)
            return Result.Fail("Sprint not found.");
        if (!await ProjectAccess.CanWriteAsync(_context, sprint.ProjectId, userId))
            return Result.Fail("You have read-only access to this project.");

        // Tasks fall back to the backlog (FK is SetNull), so clear them explicitly for the in-memory graph too.
        _context.Sprints.Remove(sprint);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Sprint deleted. SprintId: {SprintId}", sprintId);
        return Result.Ok();
    }

    /// <inheritdoc />
    public async Task<Result> AssignTaskAsync(Guid userId, Guid taskId, Guid? sprintId)
    {
        var task = await _context.ProjectTasks.FirstOrDefaultAsync(t => t.Id == taskId);
        if (task is null)
            return Result.Fail("Task not found.");
        if (!await ProjectAccess.CanWriteAsync(_context, task.ProjectId, userId))
            return Result.Fail("You have read-only access to this project.");

        if (sprintId is { } sid)
        {
            var sameProject = await _context.Sprints.AnyAsync(s => s.Id == sid && s.ProjectId == task.ProjectId);
            if (!sameProject)
                return Result.Fail("Sprint not found in this project.");
        }

        task.SprintId = sprintId;
        task.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return Result.Ok();
    }

    // --- helpers ---

    private static SprintDto MapDto(Sprint s, int taskCount, int doneCount) => new()
    {
        Id = s.Id,
        ProjectId = s.ProjectId,
        Name = s.Name,
        Goal = s.Goal,
        StartDate = s.StartDate,
        EndDate = s.EndDate,
        Status = s.Status.ToString(),
        TaskCount = taskCount,
        DoneCount = doneCount,
    };

    private static SprintStatus? ParseStatus(string? value) =>
        Enum.TryParse<SprintStatus>(value, ignoreCase: true, out var s) ? s : null;
}
