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
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? EditedAt { get; set; }
    public bool IsDeleted { get; set; }

    // Attached file (null when the message has no attachment).
    public Guid? FileId { get; set; }
    public string? FileName { get; set; }
    public string? FileContentType { get; set; }
}
