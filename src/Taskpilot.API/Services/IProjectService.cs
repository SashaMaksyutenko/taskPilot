using Taskpilot.API.Common;
using Taskpilot.API.DTOs.Projects;

namespace Taskpilot.API.Services;

/// <summary>
/// Business logic for projects (create, list, update, archive/restore).
/// For now a project is private to its owner.
/// </summary>
public interface IProjectService
{
    Task<Result<ProjectDto>> CreateProjectAsync(Guid ownerId, SaveProjectDto dto);

    /// <summary>Lists the owner's projects (active only unless includeArchived).</summary>
    Task<Result<List<ProjectDto>>> GetProjectsAsync(Guid ownerId, bool includeArchived);

    Task<Result<ProjectDto>> GetProjectAsync(Guid projectId, Guid userId);

    Task<Result<ProjectDto>> UpdateProjectAsync(Guid ownerId, Guid projectId, SaveProjectDto dto);

    /// <summary>Archives (archived = true) or restores (false) a project.</summary>
    Task<Result> SetArchivedAsync(Guid ownerId, Guid projectId, bool archived);
}
