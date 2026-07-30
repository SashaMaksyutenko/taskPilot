using Taskpilot.API.Common;
using Taskpilot.API.DTOs.Projects;

namespace Taskpilot.API.Services;

/// <summary>Sprints / iterations for a project: CRUD plus moving tasks in and out of a sprint.</summary>
public interface ISprintService
{
    /// <summary>Lists a project's sprints with task tallies (any member).</summary>
    Task<Result<List<SprintDto>>> GetSprintsAsync(Guid userId, Guid projectId);

    /// <summary>Creates a sprint in the project (owner/Editor).</summary>
    Task<Result<SprintDto>> CreateSprintAsync(Guid userId, Guid projectId, SaveSprintDto dto);

    /// <summary>Updates a sprint's fields, including its status (owner/Editor).</summary>
    Task<Result<SprintDto>> UpdateSprintAsync(Guid userId, Guid sprintId, SaveSprintDto dto);

    /// <summary>Deletes a sprint; its tasks fall back to the backlog (owner/Editor).</summary>
    Task<Result> DeleteSprintAsync(Guid userId, Guid sprintId);

    /// <summary>Moves a task into a sprint, or out to the backlog when sprintId is null (owner/Editor).</summary>
    Task<Result> AssignTaskAsync(Guid userId, Guid taskId, Guid? sprintId);
}
