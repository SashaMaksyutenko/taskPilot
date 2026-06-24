namespace Taskpilot.API.DTOs.Chat;

/// <summary>
/// Input for sending a new message to a conversation.
/// </summary>
public class SendMessageDto
{
    /// <summary>Conversation the message is posted to.</summary>
    public Guid ConversationId { get; set; }

    /// <summary>Message text. May be empty when a file is attached.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Optional id of a previously uploaded file to attach.</summary>
    public Guid? FileAttachmentId { get; set; }
}
