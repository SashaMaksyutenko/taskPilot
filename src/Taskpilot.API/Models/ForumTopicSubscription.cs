namespace Taskpilot.API.Models;

/// <summary>
/// A user's subscription to a forum topic — they get notified of every new reply,
/// not just replies to their own posts.
/// </summary>
public class ForumTopicSubscription
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Topic being followed (foreign key).</summary>
    public Guid TopicId { get; set; }

    /// <summary>Navigation to the topic.</summary>
    public ForumTopic Topic { get; set; } = null!;

    /// <summary>Subscribing user (foreign key).</summary>
    public Guid UserId { get; set; }

    /// <summary>Navigation to the user.</summary>
    public User User { get; set; } = null!;

    /// <summary>UTC time the subscription was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
