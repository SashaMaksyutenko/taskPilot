namespace Taskpilot.API.DTOs.Chat;

/// <summary>
/// Input for opening (or creating) a direct 1:1 conversation with another user.
/// </summary>
public class StartDirectConversationDto
{
    /// <summary>Id of the user to chat with.</summary>
    public Guid OtherUserId { get; set; }
}
