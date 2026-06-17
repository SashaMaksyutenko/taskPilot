namespace Taskpilot.API.DTOs.Chat;

/// <summary>
/// Input for creating a group conversation. The creator is added automatically.
/// </summary>
public class CreateGroupConversationDto
{
    /// <summary>Display name of the group.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Ids of the users to add (besides the creator).</summary>
    public List<Guid> ParticipantIds { get; set; } = new();
}
