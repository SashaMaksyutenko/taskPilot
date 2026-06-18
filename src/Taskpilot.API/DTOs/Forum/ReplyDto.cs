namespace Taskpilot.API.DTOs.Forum;

/// <summary>A forum reply as returned to clients.</summary>
public class ReplyDto
{
    public Guid Id { get; set; }
    public Guid TopicId { get; set; }
    public Guid AuthorId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public Guid? ParentReplyId { get; set; }
    public bool IsSolution { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
