namespace Taskpilot.API.Models;

/// <summary>
/// Records a user's view of a forum topic within a short time window. A unique index on
/// (TopicId, UserId, TimeBucket) makes each open count exactly one view: an accidental
/// rapid double-request lands in the same time bucket and is collapsed to one, while a
/// genuine re-open later falls in a new bucket and counts again. The user is stored by id
/// (no navigation) to avoid an extra cascade path to Users.
/// </summary>
public class ForumTopicView
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>The viewed topic (foreign key; views are removed with the topic).</summary>
    public Guid TopicId { get; set; }

    /// <summary>Navigation to the topic.</summary>
    public ForumTopic Topic { get; set; } = null!;

    /// <summary>The user who viewed the topic.</summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// The time bucket of the view (UTC ticks divided by the dedup window). Views by the
    /// same user in the same bucket are the same open and count once.
    /// </summary>
    public long TimeBucket { get; set; }

    /// <summary>UTC time of the view.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
