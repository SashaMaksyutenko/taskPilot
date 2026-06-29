namespace Taskpilot.API.Models;

/// <summary>Lifecycle state of a moderation appeal.</summary>
public enum AppealStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2
}
