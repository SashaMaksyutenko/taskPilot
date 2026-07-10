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

    /// <summary>True if any non-deleted reply is marked as the accepted solution.</summary>
    public bool IsSolved { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>Time of the latest reply, or the creation time if there are none.</summary>
    public DateTime LastActivityAt { get; set; }

    /// <summary>Free-form tags on the topic.</summary>
    public List<string> Tags { get; set; } = new();
}
