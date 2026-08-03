using Microsoft.EntityFrameworkCore;
using Taskpilot.API.Common;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Projects;
using Taskpilot.API.Models;

namespace Taskpilot.API.Services;

/// <inheritdoc />
public class EpicService : IEpicService
{
    private readonly TaskpilotDbContext _context;
    private readonly ILogger<EpicService> _logger;

    public EpicService(TaskpilotDbContext context, ILogger<EpicService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<List<EpicDto>>> GetEpicsAsync(Guid userId, Guid projectId)
    {
        if (!await ProjectAccess.CanAccessAsync(_context, projectId, userId))
            return Result<List<EpicDto>>.Fail("Project not found.");

        var epics = await _context.Epics
            .Where(e => e.ProjectId == projectId)
            .OrderBy(e => e.CreatedAt)
            .ToListAsync();

        // Task tallies per epic in one query.
        var counts = (await _context.ProjectTasks
                .Where(t => t.ProjectId == projectId && t.EpicId != null)
                .GroupBy(t => t.EpicId!.Value)
                .Select(g => new
                {
                    EpicId = g.Key,
                    Total = g.Count(),
                    Done = g.Count(t => t.Status == ProjectTaskStatus.Done),
                })
                .ToListAsync())
            .ToDictionary(c => c.EpicId);

        return Result<List<EpicDto>>.Ok(epics.Select(e =>
        {
            counts.TryGetValue(e.Id, out var c);
            return MapDto(e, c?.Total ?? 0, c?.Done ?? 0);
        }).ToList());
    }

    /// <inheritdoc />
    public async Task<Result<EpicDto>> CreateEpicAsync(Guid userId, Guid projectId, SaveEpicDto dto)
    {
        if (!await ProjectAccess.CanAccessAsync(_context, projectId, userId))
            return Result<EpicDto>.Fail("Project not found.");
        if (!await ProjectAccess.CanWriteAsync(_context, projectId, userId))
            return Result<EpicDto>.Fail("You have read-only access to this project.");
        if (string.IsNullOrWhiteSpace(dto.Title))
            return Result<EpicDto>.Fail("Epic title is required.");

        var epic = new Epic
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Title = dto.Title.Trim(),
            Color = string.IsNullOrWhiteSpace(dto.Color) ? null : dto.Color.Trim(),
            CreatedAt = DateTime.UtcNow,
        };
        _context.Epics.Add(epic);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Epic created. EpicId: {EpicId}, ProjectId: {ProjectId}", epic.Id, projectId);
        return Result<EpicDto>.Ok(MapDto(epic, 0, 0));
    }

    /// <inheritdoc />
    public async Task<Result<EpicDto>> UpdateEpicAsync(Guid userId, Guid epicId, SaveEpicDto dto)
    {
        var epic = await _context.Epics.FirstOrDefaultAsync(e => e.Id == epicId);
        if (epic is null)
            return Result<EpicDto>.Fail("Epic not found.");
        if (!await ProjectAccess.CanWriteAsync(_context, epic.ProjectId, userId))
            return Result<EpicDto>.Fail("You have read-only access to this project.");
        if (string.IsNullOrWhiteSpace(dto.Title))
            return Result<EpicDto>.Fail("Epic title is required.");

        epic.Title = dto.Title.Trim();
        epic.Color = string.IsNullOrWhiteSpace(dto.Color) ? null : dto.Color.Trim();
        await _context.SaveChangesAsync();

        var tasks = await _context.ProjectTasks.Where(t => t.EpicId == epicId).Select(t => t.Status).ToListAsync();
        return Result<EpicDto>.Ok(MapDto(epic, tasks.Count, tasks.Count(s => s == ProjectTaskStatus.Done)));
    }

    /// <inheritdoc />
    public async Task<Result> DeleteEpicAsync(Guid userId, Guid epicId)
    {
        var epic = await _context.Epics.FirstOrDefaultAsync(e => e.Id == epicId);
        if (epic is null)
            return Result.Fail("Epic not found.");
        if (!await ProjectAccess.CanWriteAsync(_context, epic.ProjectId, userId))
            return Result.Fail("You have read-only access to this project.");

        // Tasks fall back to ungrouped (FK is SetNull).
        _context.Epics.Remove(epic);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Epic deleted. EpicId: {EpicId}", epicId);
        return Result.Ok();
    }

    /// <inheritdoc />
    public async Task<Result> AssignTaskAsync(Guid userId, Guid taskId, Guid? epicId)
    {
        var task = await _context.ProjectTasks.FirstOrDefaultAsync(t => t.Id == taskId);
        if (task is null)
            return Result.Fail("Task not found.");
        if (!await ProjectAccess.CanWriteAsync(_context, task.ProjectId, userId))
            return Result.Fail("You have read-only access to this project.");

        if (epicId is { } eid)
        {
            var sameProject = await _context.Epics.AnyAsync(e => e.Id == eid && e.ProjectId == task.ProjectId);
            if (!sameProject)
                return Result.Fail("Epic not found in this project.");
        }

        task.EpicId = epicId;
        task.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return Result.Ok();
    }

    private static EpicDto MapDto(Epic e, int taskCount, int doneCount) => new()
    {
        Id = e.Id,
        ProjectId = e.ProjectId,
        Title = e.Title,
        Color = e.Color,
        TaskCount = taskCount,
        DoneCount = doneCount,
    };
}
