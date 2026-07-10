namespace Taskpilot.API.Models;

/// <summary>An emoji reaction a user placed on a forum reply.</summary>
public class ForumReplyReaction
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Reply being reacted to (foreign key).</summary>
    public Guid ReplyId { get; set; }

    /// <summary>Navigation to the reply.</summary>
    public ForumReply Reply { get; set; } = null!;

    /// <summary>User who reacted (foreign key).</summary>
    public Guid UserId { get; set; }

    /// <summary>Navigation to the reacting user.</summary>
    public User User { get; set; } = null!;

    /// <summary>The emoji (e.g. "👍").</summary>
    public string Emoji { get; set; } = string.Empty;

    /// <summary>UTC time the reaction was added.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
