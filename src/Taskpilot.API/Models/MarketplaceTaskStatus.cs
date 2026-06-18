namespace Taskpilot.API.Models;

/// <summary>Lifecycle status of a marketplace task.</summary>
public enum MarketplaceTaskStatus
{
    /// <summary>Posted and accepting applications.</summary>
    Open = 0,

    /// <summary>An applicant was accepted and is working on it.</summary>
    InProgress = 1,

    /// <summary>Work was submitted and approved.</summary>
    Completed = 2,

    /// <summary>Cancelled by the poster.</summary>
    Cancelled = 3
}
