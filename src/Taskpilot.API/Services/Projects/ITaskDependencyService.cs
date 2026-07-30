using Taskpilot.API.Common;
using Taskpilot.API.DTOs.Projects;

namespace Taskpilot.API.Services;

/// <summary>
/// Task dependencies ("blocked by" / "blocks"). Adding a dependency is validated to keep the
/// graph within one project and acyclic. Used by the board/Gantt to show what blocks what.
/// </summary>
public interface ITaskDependencyService
{
    /// <summary>Returns a task's dependency graph (what it depends on, what it blocks, and whether it's blocked).</summary>
    Task<Result<TaskDependenciesDto>> GetAsync(Guid userId, Guid taskId);

    /// <summary>Adds "taskId depends on dependsOnTaskId" (same project, no self, no duplicate, no cycle).</summary>
    Task<Result<TaskDependenciesDto>> AddAsync(Guid userId, Guid taskId, Guid dependsOnTaskId);

    /// <summary>Removes a dependency edge.</summary>
    Task<Result> RemoveAsync(Guid userId, Guid taskId, Guid dependsOnTaskId);

    /// <summary>Computes the project's critical path (longest chain of dependent tasks).</summary>
    Task<Result<CriticalPathDto>> GetCriticalPathAsync(Guid userId, Guid projectId);
}
