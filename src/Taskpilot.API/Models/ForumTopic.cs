namespace Taskpilot.API.Models;

/// <summary>
/// A forum discussion topic started by a user. Holds the original post and its replies.
/// </summary>
public class ForumTopic
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Topic title (the headline shown in lists).</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Body of the original post.</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>User who created the topic (foreign key).</summary>
    public Guid AuthorId { get; set; }

    /// <summary>Navigation to the author.</summary>
    public User Author { get; set; } = null!;

    /// <summary>Number of times the topic has been viewed.</summary>
    public int ViewCount { get; set; }

    /// <summary>Pinned topics are shown on top (admin/moderator action).</summary>
    public bool IsPinned { get; set; }

    /// <summary>Locked topics accept no new replies.</summary>
    public bool IsLocked { get; set; }

    /// <summary>UTC time the topic was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>UTC time the topic was last edited; null if never.</summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>Free-form tags for categorising and filtering topics.</summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>Replies posted to this topic.</summary>
    public ICollection<ForumReply> Replies { get; set; } = new List<ForumReply>();
}
