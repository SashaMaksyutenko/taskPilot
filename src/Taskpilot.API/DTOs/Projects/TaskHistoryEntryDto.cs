namespace Taskpilot.API.DTOs.Projects;

/// <summary>
/// One line of a task's history, as shown to anyone with access to the task's project.
/// Deliberately narrower than the admin's audit view: the actor's email and IP address
/// are NOT exposed here, because teammates may read this and an email is private
/// unless its owner opted into sharing it.
/// </summary>
public class TaskHistoryEntryDto
{
    /// <summary>Primary key of the underlying audit entry.</summary>
    public Guid Id { get; set; }

    /// <summary>Stable dotted action code, e.g. "task.status.changed".</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Id of the user who performed the action; null for system actions.</summary>
    public Guid? ActorId { get; set; }

    /// <summary>
    /// Display name of the actor at read time; null when the action was performed by
    /// the system or the account has since been removed.
    /// </summary>
    public string? ActorName { get; set; }

    /// <summary>Human-readable description of what changed (e.g. "Status: Backlog → Done").</summary>
    public string? Details { get; set; }

    /// <summary>UTC time the action occurred.</summary>
    public DateTime CreatedAt { get; set; }
}
