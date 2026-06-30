namespace Taskpilot.API.DTOs.Projects;

/// <summary>Payload to add a collaborator to a project.</summary>
public class AddMemberDto
{
    public Guid UserId { get; set; }

    /// <summary>"Editor" (default) or "Viewer".</summary>
    public string? Role { get; set; }
}

/// <summary>Payload to change a member's role.</summary>
public class SetMemberRoleDto
{
    public string Role { get; set; } = string.Empty;
}
