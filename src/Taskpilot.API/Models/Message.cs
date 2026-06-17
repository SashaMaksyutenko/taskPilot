namespace Taskpilot.API.Models;

/// <summary>
/// A single chat message posted by a user in a conversation.
/// </summary>
public class Message
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Conversation the message belongs to (foreign key).</summary>
    public Guid ConversationId { get; set; }

    /// <summary>Navigation to the conversation.</summary>
    public Conversation Conversation { get; set; } = null!;

    /// <summary>User who sent the message (foreign key).</summary>
    public Guid SenderId { get; set; }

    /// <summary>Navigation to the sender.</summary>
    public User Sender { get; set; } = null!;

    /// <summary>Message text.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>UTC time the message was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>UTC time the message was last edited; null if never edited.</summary>
    public DateTime? EditedAt { get; set; }

    /// <summary>Soft-delete flag: true means the message was deleted but kept for history.</summary>
    public bool IsDeleted { get; set; }
}
