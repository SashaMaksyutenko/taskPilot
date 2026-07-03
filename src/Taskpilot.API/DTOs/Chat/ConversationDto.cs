namespace Taskpilot.API.DTOs.Chat;

/// <summary>
/// A conversation as returned to clients, with its participants.
/// </summary>
public class ConversationDto
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string? Name { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<ParticipantDto> Participants { get; set; } = new();

    /// <summary>Number of messages in this conversation the current user has not read yet.</summary>
    public int UnreadCount { get; set; }
}
