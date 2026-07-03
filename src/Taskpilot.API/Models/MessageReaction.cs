namespace Taskpilot.API.Models;

/// <summary>An emoji reaction a user placed on a chat message.</summary>
public class MessageReaction
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Message being reacted to (foreign key).</summary>
    public Guid MessageId { get; set; }

    /// <summary>Navigation to the message.</summary>
    public Message Message { get; set; } = null!;

    /// <summary>User who reacted (foreign key).</summary>
    public Guid UserId { get; set; }

    /// <summary>Navigation to the reacting user.</summary>
    public User User { get; set; } = null!;

    /// <summary>The emoji (e.g. "👍").</summary>
    public string Emoji { get; set; } = string.Empty;

    /// <summary>UTC time the reaction was added.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
