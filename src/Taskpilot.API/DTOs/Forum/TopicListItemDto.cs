namespace Taskpilot.API.DTOs.Forum;

/// <summary>A forum topic as shown in the topic list (without the full body or replies).</summary>
public class TopicListItemDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public Guid AuthorId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string? AuthorAvatarUrl { get; set; }
    public int ViewCount { get; set; }
    public int ReplyCount { get; set; }
    public bool IsPinned { get; set; }
    public bool IsLocked { get; set; }
    public DateTime CreatedAt { get; set; }
}
