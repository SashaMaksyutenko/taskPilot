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

    /// <summary>Whether the current user has muted notifications from this conversation.</summary>
    public bool Muted { get; set; }
}

/// <summary>Input for muting or unmuting a conversation.</summary>
public class SetMutedDto
{
    /// <summary>True to mute the conversation, false to unmute it.</summary>
    public bool Muted { get; set; }
}
