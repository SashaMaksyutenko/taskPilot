using Taskpilot.API.Common;
using Taskpilot.API.DTOs.Projects;

namespace Taskpilot.API.Services;

/// <summary>
/// Comments on project tasks. A task is reachable only through a project the caller
/// owns, so every operation verifies that ownership first.
/// </summary>
public interface ITaskCommentService
{
    /// <summary>Lists a task's comments oldest-first.</summary>
    Task<Result<List<TaskCommentDto>>> GetForTaskAsync(Guid userId, Guid taskId);

    /// <summary>Adds a comment authored by the caller to a task.</summary>
    Task<Result<TaskCommentDto>> AddAsync(Guid userId, Guid taskId, CreateCommentDto dto);

    /// <summary>Deletes a comment the caller authored; returns the task id for broadcasting.</summary>
    Task<Result<Guid>> DeleteAsync(Guid userId, Guid commentId);
}
