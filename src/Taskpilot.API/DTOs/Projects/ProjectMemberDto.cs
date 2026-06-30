namespace Taskpilot.API.DTOs.Projects;

/// <summary>A project collaborator as returned to clients.</summary>
public class ProjectMemberDto
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }

    /// <summary>Permission level: "Editor" or "Viewer" (owner is always full access).</summary>
    public string Role { get; set; } = "Editor";

    /// <summary>True for the project owner (shown but not removable).</summary>
    public bool IsOwner { get; set; }
}
