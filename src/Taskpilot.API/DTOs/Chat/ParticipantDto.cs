namespace Taskpilot.API.DTOs.Chat;

/// <summary>
/// Minimal user info for a conversation participant.
/// </summary>
public class ParticipantDto
{
    /// <summary>Id of the participating user.</summary>
    public Guid UserId { get; set; }

    /// <summary>Display name of the participating user.</summary>
    public string Name { get; set; } = string.Empty;
}
