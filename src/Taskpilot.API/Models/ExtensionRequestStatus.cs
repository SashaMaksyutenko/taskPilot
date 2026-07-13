namespace Taskpilot.API.Models;

/// <summary>Lifecycle of a task deadline-extension request. Stored as a string.</summary>
public enum ExtensionRequestStatus
{
    /// <summary>Awaiting the project owner's decision.</summary>
    Pending,

    /// <summary>Approved — the task deadline was moved to the requested date.</summary>
    Approved,

    /// <summary>Rejected — the deadline is unchanged.</summary>
    Rejected,
}
