namespace Taskpilot.API.DTOs.Chat;

/// <summary>
/// Input for sending a new message to a conversation.
/// </summary>
public class SendMessageDto
{
    /// <summary>Conversation the message is posted to.</summary>
    public Guid ConversationId { get; set; }

    /// <summary>Message text.</summary>
    public string Content { get; set; } = string.Empty;
}
