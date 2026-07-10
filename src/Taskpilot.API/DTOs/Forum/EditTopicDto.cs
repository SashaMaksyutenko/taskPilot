namespace Taskpilot.API.DTOs.Forum;

/// <summary>Input for editing an existing topic's title and body.</summary>
public class EditTopicDto
{
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}
