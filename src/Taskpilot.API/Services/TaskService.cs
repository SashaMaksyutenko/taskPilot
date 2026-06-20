using Microsoft.EntityFrameworkCore;
using Taskpilot.API.Common;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Projects;
using Taskpilot.API.Models;

namespace Taskpilot.API.Services;

/// <summary>
/// Handles project-task business logic. A task is reachable only through a project
/// the user owns, so every operation checks that ownership first.
/// </summary>
public class TaskService : ITaskService
{
    private readonly TaskpilotDbContext _context;
    private readonly ILogger<TaskService> _logger;

    public TaskService(TaskpilotDbContext context, ILogger<TaskService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<TaskDto>> CreateTaskAsync(Guid userId, Guid projectId, CreateTaskDto dto)
    {
        var ownsProject = await _context.Projects.AnyAsync(p => p.Id == projectId && p.OwnerId == userId);
        if (!ownsProject)
            return Result<TaskDto>.Fail("Project not found.");

        // Priority defaults to Medium.
        var priority = TaskPriority.Medium;
        if (!string.IsNullOrWhiteSpace(dto.Priority) &&
            !Enum.TryParse(dto.Priority, ignoreCase: true, out priority))
            return Result<TaskDto>.Fail("Invalid priority.");

        // Optional assignee must exist.
        if (dto.AssigneeId.HasValue &&
            !await _context.Users.AnyAsync(u => u.Id == dto.AssigneeId.Value))
            return Result<TaskDto>.Fail("Assignee not found.");

        // Optional parent task must belong to the same project.
        if (dto.ParentTaskId.HasValue &&
            !await _context.ProjectTasks.AnyAsync(t => t.Id == dto.ParentTaskId.Value && t.ProjectId == projectId))
            return Result<TaskDto>.Fail("Parent task not found in this project.");

        var task = new ProjectTask
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Title = dto.Title.Trim(),
            Description = dto.Description?.Trim(),
            Status = ProjectTaskStatus.Backlog,
            Priority = priority,
            AssigneeId = dto.AssigneeId,
            CreatorId = userId,
            ParentTaskId = dto.ParentTaskId,
            Deadline = dto.Deadline,
            CreatedAt = DateTime.UtcNow,
        };
        _context.ProjectTasks.Add(task);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Task created. TaskId: {TaskId}, ProjectId: {ProjectId}", task.Id, projectId);
        return Result<TaskDto>.Ok(await LoadDtoAsync(task.Id));
    }

    /// <inheritdoc />
    public async Task<Result<List<TaskDto>>> GetTasksAsync(Guid userId, Guid projectId, string? status)
    {
        var ownsProject = await _context.Projects.AnyAsync(p => p.Id == projectId && p.OwnerId == userId);
        if (!ownsProject)
            return Result<List<TaskDto>>.Fail("Project not found.");

        ProjectTaskStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<ProjectTaskStatus>(status, ignoreCase: true, out var parsed))
                return Result<List<TaskDto>>.Fail("Invalid status.");
            statusFilter = parsed;
        }

        var tasks = await _context.ProjectTasks
            .Where(t => t.ProjectId == projectId && (statusFilter == null || t.Status == statusFilter))
            .Include(t => t.Assignee)
            .Include(t => t.Creator)
            .OrderBy(t => t.CreatedAt)
            .AsNoTracking()
            .ToListAsync();

        return Result<List<TaskDto>>.Ok(tasks.Select(MapDto).ToList());
    }

    /// <inheritdoc />
    public async Task<Result<TaskDto>> GetTaskAsync(Guid userId, Guid taskId)
    {
        var task = await LoadOwnedAsync(taskId, userId);
        return task is null
            ? Result<TaskDto>.Fail("Task not found.")
            : Result<TaskDto>.Ok(MapDto(task));
    }

    /// <inheritdoc />
    public async Task<Result<TaskDto>> UpdateTaskAsync(Guid userId, Guid taskId, UpdateTaskDto dto)
    {
        var task = await LoadOwnedAsync(taskId, userId);
        if (task is null)
            return Result<TaskDto>.Fail("Task not found.");

        var priority = task.Priority;
        if (!string.IsNullOrWhiteSpace(dto.Priority) &&
            !Enum.TryParse(dto.Priority, ignoreCase: true, out priority))
            return Result<TaskDto>.Fail("Invalid priority.");

        if (dto.AssigneeId.HasValue &&
            !await _context.Users.AnyAsync(u => u.Id == dto.AssigneeId.Value))
            return Result<TaskDto>.Fail("Assignee not found.");

        task.Title = dto.Title.Trim();
        task.Description = dto.Description?.Trim();
        task.Priority = priority;
        task.AssigneeId = dto.AssigneeId;
        task.Deadline = dto.Deadline;
        task.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Result<TaskDto>.Ok(await LoadDtoAsync(task.Id));
    }

    /// <inheritdoc />
    public async Task<Result<TaskDto>> ChangeStatusAsync(Guid userId, Guid taskId, string status)
    {
        if (!Enum.TryParse<ProjectTaskStatus>(status, ignoreCase: true, out var parsed))
            return Result<TaskDto>.Fail("Invalid status.");

        var task = await LoadOwnedAsync(taskId, userId);
        if (task is null)
            return Result<TaskDto>.Fail("Task not found.");

        task.Status = parsed;
        // Track completion time when moving to/away from Done.
        task.CompletedAt = parsed == ProjectTaskStatus.Done ? DateTime.UtcNow : null;
        task.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Task status changed. TaskId: {TaskId}, Status: {Status}", taskId, parsed);
        return Result<TaskDto>.Ok(await LoadDtoAsync(task.Id));
    }

    /// <inheritdoc />
    public async Task<Result> DeleteTaskAsync(Guid userId, Guid taskId)
    {
        var task = await LoadOwnedAsync(taskId, userId);
        if (task is null)
            return Result.Fail("Task not found.");

        _context.ProjectTasks.Remove(task);
        await _context.SaveChangesAsync();
        return Result.Ok();
    }

    // --- helpers ---

    /// <summary>Loads a task (with assignee/creator) only if the caller owns its project.</summary>
    private async Task<ProjectTask?> LoadOwnedAsync(Guid taskId, Guid userId)
    {
        var task = await _context.ProjectTasks
            .Include(t => t.Project)
            .Include(t => t.Assignee)
            .Include(t => t.Creator)
            .FirstOrDefaultAsync(t => t.Id == taskId);

        return task is not null && task.Project.OwnerId == userId ? task : null;
    }

    /// <summary>Reloads a task as a DTO (with assignee/creator names).</summary>
    private async Task<TaskDto> LoadDtoAsync(Guid taskId)
    {
        var task = await _context.ProjectTasks
            .Include(t => t.Assignee)
            .Include(t => t.Creator)
            .AsNoTracking()
            .FirstAsync(t => t.Id == taskId);
        return MapDto(task);
    }

    private static TaskDto MapDto(ProjectTask t) => new()
    {
        Id = t.Id,
        ProjectId = t.ProjectId,
        Title = t.Title,
        Description = t.Description,
        Status = t.Status.ToString(),
        Priority = t.Priority.ToString(),
        AssigneeId = t.AssigneeId,
        AssigneeName = t.Assignee?.Name,
        CreatorId = t.CreatorId,
        CreatorName = t.Creator?.Name ?? string.Empty,
        ParentTaskId = t.ParentTaskId,
        Deadline = t.Deadline,
        CreatedAt = t.CreatedAt,
        UpdatedAt = t.UpdatedAt,
        CompletedAt = t.CompletedAt,
    };
}
