namespace Taskpilot.API.DTOs.Notifications;

/// <summary>Replaces the user's notification opt-outs.</summary>
public class UpdatePreferencesDto
{
    /// <summary>Notification type names the user wants to disable (others stay enabled).</summary>
    public List<string> DisabledTypes { get; set; } = new();
}
