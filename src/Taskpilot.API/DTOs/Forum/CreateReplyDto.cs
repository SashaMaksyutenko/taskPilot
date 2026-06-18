namespace Taskpilot.API.DTOs.Forum;

/// <summary>Input for posting a reply to a topic (optionally nested under another reply).</summary>
public class CreateReplyDto
{
    public Guid TopicId { get; set; }
    public string Body { get; set; } = string.Empty;

    /// <summary>Optional parent reply id for nested/threaded replies.</summary>
    public Guid? ParentReplyId { get; set; }
}
