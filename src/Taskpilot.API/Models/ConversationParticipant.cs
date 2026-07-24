namespace Taskpilot.API.Models;

/// <summary>
/// Join entity linking a <see cref="User"/> to a <see cref="Conversation"/>
/// (many-to-many membership).
/// </summary>
public class ConversationParticipant
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Conversation the user belongs to (foreign key).</summary>
    public Guid ConversationId { get; set; }

    /// <summary>Navigation to the conversation.</summary>
    public Conversation Conversation { get; set; } = null!;

    /// <summary>Participating user (foreign key).</summary>
    public Guid UserId { get; set; }

    /// <summary>Navigation to the user.</summary>
    public User User { get; set; } = null!;

    /// <summary>UTC time the user joined the conversation.</summary>
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    /// <summary>UTC time the user last read this conversation; null if never. Used for unread counts.</summary>
    public DateTime? LastReadAt { get; set; }

    /// <summary>
    /// When true, this user has muted the conversation: they still see its messages, but get
    /// no notifications about new messages or mentions in it (spec module 6).
    /// </summary>
    public bool Muted { get; set; }
}
