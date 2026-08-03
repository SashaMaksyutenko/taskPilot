using Taskpilot.API.Common;
using Taskpilot.API.DTOs.Projects;

namespace Taskpilot.API.Services;

/// <summary>Reads a project's activity feed from the task audit trail.</summary>
public interface IActivityService
{
    /// <summary>Recent task actions in a project, newest first (any project member).</summary>
    Task<Result<List<ActivityEntryDto>>> GetProjectActivityAsync(Guid userId, Guid projectId, int limit = 30);
}
