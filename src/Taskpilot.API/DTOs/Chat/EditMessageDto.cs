namespace Taskpilot.API.DTOs.Chat;

/// <summary>
/// Input for editing an existing message's text.
/// </summary>
public class EditMessageDto
{
    /// <summary>New message text (must be non-empty).</summary>
    public string Content { get; set; } = string.Empty;
}
