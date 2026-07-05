namespace Taskpilot.API.DTOs.Notifications;

/// <summary>Replaces the user's notification opt-outs.</summary>
public class UpdatePreferencesDto
{
    /// <summary>Notification type names muted for the in-app channel (others stay enabled).</summary>
    public List<string> DisabledTypes { get; set; } = new();

    /// <summary>Notification type names muted for email delivery (others stay enabled).</summary>
    public List<string> DisabledEmailTypes { get; set; } = new();
}
