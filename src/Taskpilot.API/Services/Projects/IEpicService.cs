using Taskpilot.API.Common;
using Taskpilot.API.DTOs.Projects;

namespace Taskpilot.API.Services;

/// <summary>Manages a project's epics and which epic each task belongs to.</summary>
public interface IEpicService
{
    /// <summary>Lists a project's epics with task tallies (any member).</summary>
    Task<Result<List<EpicDto>>> GetEpicsAsync(Guid userId, Guid projectId);

    /// <summary>Creates an epic (owner/Editor).</summary>
    Task<Result<EpicDto>> CreateEpicAsync(Guid userId, Guid projectId, SaveEpicDto dto);

    /// <summary>Renames/recolours an epic (owner/Editor).</summary>
    Task<Result<EpicDto>> UpdateEpicAsync(Guid userId, Guid epicId, SaveEpicDto dto);

    /// <summary>Deletes an epic; its tasks fall back to ungrouped (owner/Editor).</summary>
    Task<Result> DeleteEpicAsync(Guid userId, Guid epicId);

    /// <summary>Moves a task into an epic, or ungroups it when epicId is null (owner/Editor).</summary>
    Task<Result> AssignTaskAsync(Guid userId, Guid taskId, Guid? epicId);
}
