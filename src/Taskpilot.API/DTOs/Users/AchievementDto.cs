namespace Taskpilot.API.DTOs.Users;

/// <summary>
/// One achievement badge for a user, computed on the fly from real activity (task
/// completions and the reputation ledger). Name/description/icon live on the client,
/// keyed by <see cref="Code"/>, so they can be localized.
/// </summary>
public class AchievementDto
{
    /// <summary>Stable badge id, e.g. "ten_tasks".</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>True once <see cref="Current"/> reaches <see cref="Target"/>.</summary>
    public bool Earned { get; set; }

    /// <summary>Current progress value (uncapped).</summary>
    public int Current { get; set; }

    /// <summary>Value needed to earn the badge.</summary>
    public int Target { get; set; }
}
