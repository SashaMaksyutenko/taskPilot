namespace Taskpilot.API.DTOs.Forum;

/// <summary>A forum topic with its full body and replies (the topic detail view).</summary>
public class TopicDetailDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public Guid AuthorId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string? AuthorAvatarUrl { get; set; }
    public int ViewCount { get; set; }
    public bool IsPinned { get; set; }
    public bool IsLocked { get; set; }

    /// <summary>Whether the current user is subscribed to (following) this topic.</summary>
    public bool IsSubscribed { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<ReplyDto> Replies { get; set; } = new();
}
