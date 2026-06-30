namespace Taskpilot.API.DTOs.Projects;

/// <summary>Payload to add a collaborator to a project.</summary>
public class AddMemberDto
{
    public Guid UserId { get; set; }
}
