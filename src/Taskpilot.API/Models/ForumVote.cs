namespace Taskpilot.API.Models;

/// <summary>
/// A user's vote on a forum reply. <see cref="Value"/> is +1 (upvote) or -1 (downvote).
/// A user can have at most one vote per reply.
/// </summary>
public class ForumVote
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Reply being voted on (foreign key).</summary>
    public Guid ReplyId { get; set; }

    /// <summary>Navigation to the reply.</summary>
    public ForumReply Reply { get; set; } = null!;

    /// <summary>User who voted (foreign key).</summary>
    public Guid UserId { get; set; }

    /// <summary>Navigation to the voter.</summary>
    public User User { get; set; } = null!;

    /// <summary>+1 for an upvote, -1 for a downvote.</summary>
    public int Value { get; set; }

    /// <summary>UTC time the vote was cast or last changed.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
