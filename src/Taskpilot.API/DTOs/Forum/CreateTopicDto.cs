namespace Taskpilot.API.DTOs.Forum;

/// <summary>Input for creating a new forum topic.</summary>
public class CreateTopicDto
{
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}
