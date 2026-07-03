namespace Taskpilot.API.DTOs.Chat;

/// <summary>
/// A chat message as returned to clients.
/// </summary>
public class MessageDto
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public Guid SenderId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string? SenderAvatarUrl { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? EditedAt { get; set; }
    public bool IsDeleted { get; set; }
    public bool IsPinned { get; set; }

    // Attached file (null when the message has no attachment).
    public Guid? FileId { get; set; }
    public string? FileName { get; set; }
    public string? FileContentType { get; set; }

    /// <summary>Emoji reactions grouped by emoji.</summary>
    public List<ReactionDto> Reactions { get; set; } = new();
}

/// <summary>A group of reactions with the same emoji on a message.</summary>
public class ReactionDto
{
    public string Emoji { get; set; } = string.Empty;
    public int Count { get; set; }

    /// <summary>Whether the current user reacted with this emoji.</summary>
    public bool Mine { get; set; }
}

/// <summary>The updated reactions for a message (used for the toggle response + realtime).</summary>
public class ReactionUpdateDto
{
    public Guid MessageId { get; set; }
    public Guid ConversationId { get; set; }
    public List<ReactionDto> Reactions { get; set; } = new();
}

/// <summary>Payload to toggle an emoji reaction.</summary>
public class ReactDto
{
    public string Emoji { get; set; } = string.Empty;
}
