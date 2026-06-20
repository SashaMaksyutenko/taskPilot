using Taskpilot.API.Common;
using Taskpilot.API.DTOs.Projects;

namespace Taskpilot.API.Services;

/// <summary>
/// Business logic for project tasks: CRUD, Kanban status changes, assignment.
/// Access is scoped through the parent project's owner.
/// </summary>
public interface ITaskService
{
    Task<Result<TaskDto>> CreateTaskAsync(Guid userId, Guid projectId, CreateTaskDto dto);

    /// <summary>Lists a project's tasks, optionally filtered by status (a Kanban column).</summary>
    Task<Result<List<TaskDto>>> GetTasksAsync(Guid userId, Guid projectId, string? status);

    Task<Result<TaskDto>> GetTaskAsync(Guid userId, Guid taskId);

    Task<Result<TaskDto>> UpdateTaskAsync(Guid userId, Guid taskId, UpdateTaskDto dto);

    /// <summary>Moves a task to another status; sets/clears CompletedAt for Done.</summary>
    Task<Result<TaskDto>> ChangeStatusAsync(Guid userId, Guid taskId, string status);

    Task<Result> DeleteTaskAsync(Guid userId, Guid taskId);
}
