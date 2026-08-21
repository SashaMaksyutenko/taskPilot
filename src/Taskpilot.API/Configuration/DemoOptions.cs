namespace Taskpilot.API.Configuration;

/// <summary>
/// Controls the no-signup demo. When enabled, an anonymous visitor can spin up a fresh, isolated
/// throwaway account (pre-seeded with sample data) with one click; expired demo accounts are
/// cleaned up in the background. Bound from the "Demo" section. Off unless explicitly turned on.
/// </summary>
public class DemoOptions
{
    /// <summary>Master switch. When false, the demo endpoints 404 and the button is hidden.</summary>
    public bool Enabled { get; set; }

    /// <summary>How long a demo account lives before the cleanup job reclaims it. Default 24h.</summary>
    public int RetentionHours { get; set; } = 24;
}
