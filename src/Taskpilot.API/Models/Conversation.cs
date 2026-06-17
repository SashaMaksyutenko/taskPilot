namespace Taskpilot.API.Models;

/// <summary>
/// A chat conversation. It can be a one-to-one <see cref="ConversationType.Direct"/>
/// chat or a <see cref="ConversationType.Group"/> chat with many participants.
/// Holds its participants and messages.
/// </summary>
public class Conversation
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Whether this is a direct (1:1) or group conversation.</summary>
    public ConversationType Type { get; set; }

    /// <summary>
    /// Display name for group conversations. Null for direct chats
    /// (their title is derived from the other participant).
    /// </summary>
    public string? Name { get; set; }

    /// <summary>UTC time the conversation was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Users taking part in this conversation.</summary>
    public ICollection<ConversationParticipant> Participants { get; set; } = new List<ConversationParticipant>();

    /// <summary>Messages posted in this conversation.</summary>
    public ICollection<Message> Messages { get; set; } = new List<Message>();
}
