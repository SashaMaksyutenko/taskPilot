namespace Taskpilot.API.DTOs.Calendar;

/// <summary>Whether Google Calendar sync is available and connected for the current user.</summary>
public class GoogleCalendarStatusDto
{
    /// <summary>True when the server has Google OAuth configured (feature available at all).</summary>
    public bool Configured { get; set; }

    /// <summary>True when this user has completed the consent flow.</summary>
    public bool Connected { get; set; }

    /// <summary>UTC time of the user's last successful push, if any.</summary>
    public DateTime? LastSyncedUtc { get; set; }
}

/// <summary>Outcome of a push sync (TaskPilot → Google).</summary>
public class GoogleCalendarSyncResultDto
{
    public int Created { get; set; }
    public int Updated { get; set; }
    public int Total { get; set; }
}

/// <summary>Outcome of a pull sync (Google → TaskPilot): tasks rescheduled from moved events.</summary>
public class GoogleCalendarPullResultDto
{
    public int Rescheduled { get; set; }
    public int Checked { get; set; }
}

/// <summary>Body for completing the Google consent flow.</summary>
public class GoogleCalendarConnectDto
{
    public string Code { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
}
