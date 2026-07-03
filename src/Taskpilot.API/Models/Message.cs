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

    /// <summary>Message text. May be empty when the message is just a file attachment.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Optional file attached to the message (foreign key).</summary>
    public Guid? FileAttachmentId { get; set; }

    /// <summary>Navigation to the attached file, if any.</summary>
    public FileAttachment? FileAttachment { get; set; }

    /// <summary>UTC time the message was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>UTC time the message was last edited; null if never edited.</summary>
    public DateTime? EditedAt { get; set; }

    /// <summary>Soft-delete flag: true means the message was deleted but kept for history.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>Pinned messages are highlighted at the top of the conversation.</summary>
    public bool IsPinned { get; set; }

    /// <summary>Emoji reactions on this message.</summary>
    public ICollection<MessageReaction> Reactions { get; set; } = new List<MessageReaction>();
}
