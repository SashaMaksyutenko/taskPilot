namespace Taskpilot.API.Models;

/// <summary>Status of a developer's application to a marketplace task.</summary>
public enum ApplicationStatus
{
    /// <summary>Submitted, awaiting the poster's decision.</summary>
    Pending = 0,

    /// <summary>Accepted by the poster (the applicant gets the task).</summary>
    Accepted = 1,

    /// <summary>Rejected by the poster.</summary>
    Rejected = 2
}
