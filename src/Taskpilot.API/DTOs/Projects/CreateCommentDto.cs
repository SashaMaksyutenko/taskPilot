namespace Taskpilot.API.DTOs.Projects;

/// <summary>Payload to add a comment to a task.</summary>
public class CreateCommentDto
{
    public string Body { get; set; } = string.Empty;
}
