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

    /// <summary>Creates a copy of a project the user owns, cloning its tasks (statuses reset to Backlog).</summary>
    Task<Result<ProjectDto>> DuplicateProjectAsync(Guid ownerId, Guid projectId);

    /// <summary>Archives (archived = true) or restores (false) a project.</summary>
    Task<Result> SetArchivedAsync(Guid ownerId, Guid projectId, bool archived);

    /// <summary>Lists a project's members (owner first); accessible to owner and members.</summary>
    Task<Result<List<ProjectMemberDto>>> GetMembersAsync(Guid userId, Guid projectId);

    /// <summary>Adds a collaborator to a project with a role (owner only).</summary>
    Task<Result<ProjectMemberDto>> AddMemberAsync(Guid ownerId, Guid projectId, Guid targetUserId, string? role);

    /// <summary>Changes a member's role (owner only).</summary>
    Task<Result<ProjectMemberDto>> SetMemberRoleAsync(Guid ownerId, Guid projectId, Guid targetUserId, string role);

    /// <summary>Removes a collaborator from a project (owner only).</summary>
    Task<Result> RemoveMemberAsync(Guid ownerId, Guid projectId, Guid targetUserId);

    /// <summary>Lets a member leave a project they collaborate on (not the owner).</summary>
    Task<Result> LeaveAsync(Guid userId, Guid projectId);
}
