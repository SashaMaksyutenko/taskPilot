namespace Taskpilot.API.DTOs.Forum;

/// <summary>Input for editing an existing reply's body.</summary>
public class EditReplyDto
{
    public string Body { get; set; } = string.Empty;
}
