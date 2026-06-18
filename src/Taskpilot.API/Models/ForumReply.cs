namespace Taskpilot.API.Models;

/// <summary>
/// A reply to a forum topic. Replies can be nested (a reply to another reply)
/// via <see cref="ParentReplyId"/>.
/// </summary>
public class ForumReply
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Topic this reply belongs to (foreign key).</summary>
    public Guid TopicId { get; set; }

    /// <summary>Navigation to the topic.</summary>
    public ForumTopic Topic { get; set; } = null!;

    /// <summary>User who wrote the reply (foreign key).</summary>
    public Guid AuthorId { get; set; }

    /// <summary>Navigation to the author.</summary>
    public User Author { get; set; } = null!;

    /// <summary>Reply text.</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// Optional parent reply for nested/threaded replies.
    /// Null for a top-level reply to the topic.
    /// </summary>
    public Guid? ParentReplyId { get; set; }

    /// <summary>Navigation to the parent reply (if any).</summary>
    public ForumReply? ParentReply { get; set; }

    /// <summary>Marks this reply as the accepted solution to the topic.</summary>
    public bool IsSolution { get; set; }

    /// <summary>UTC time the reply was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>UTC time the reply was last edited; null if never.</summary>
    public DateTime? UpdatedAt { get; set; }
}
