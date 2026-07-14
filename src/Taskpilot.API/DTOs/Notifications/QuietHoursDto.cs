namespace Taskpilot.API.DTOs.Notifications;

/// <summary>The user's quiet-hours window (local hours + their time zone).</summary>
public class QuietHoursDto
{
    /// <summary>Whether quiet hours hold back out-of-band notifications.</summary>
    public bool Enabled { get; set; }

    /// <summary>Local hour the window opens (0–23).</summary>
    public int Start { get; set; } = 22;

    /// <summary>Local hour the window closes (0–23).</summary>
    public int End { get; set; } = 8;

    /// <summary>IANA time zone id (e.g. "Europe/Kyiv"); null means UTC.</summary>
    public string? TimeZoneId { get; set; }
}
