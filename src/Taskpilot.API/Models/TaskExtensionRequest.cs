namespace Taskpilot.API.Models;

/// <summary>
/// A request to move a task's deadline to a later date. Raised by someone working on
/// the task; the project owner approves (which updates the deadline) or rejects it.
/// </summary>
public class TaskExtensionRequest
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Task the extension is for (foreign key).</summary>
    public Guid TaskId { get; set; }

    /// <summary>Navigation to the task.</summary>
    public ProjectTask Task { get; set; } = null!;

    /// <summary>User who requested the extension (foreign key).</summary>
    public Guid RequesterId { get; set; }

    /// <summary>Navigation to the requester.</summary>
    public User Requester { get; set; } = null!;

    /// <summary>The new deadline being requested (UTC).</summary>
    public DateTime RequestedDeadline { get; set; }

    /// <summary>Why the extension is needed.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>Current status of the request.</summary>
    public ExtensionRequestStatus Status { get; set; } = ExtensionRequestStatus.Pending;

    /// <summary>UTC time the request was raised.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>User who approved/rejected it; null while pending.</summary>
    public Guid? DecidedById { get; set; }

    /// <summary>UTC time the decision was made; null while pending.</summary>
    public DateTime? DecidedAt { get; set; }
}
