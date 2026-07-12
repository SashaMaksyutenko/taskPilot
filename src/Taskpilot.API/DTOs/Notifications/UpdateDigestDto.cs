namespace Taskpilot.API.DTOs.Notifications;

/// <summary>Sets the user's digest email cadence.</summary>
public class UpdateDigestDto
{
    /// <summary>One of "Off", "Daily", "Weekly".</summary>
    public string Frequency { get; set; } = "Off";
}
