namespace Taskpilot.API.DTOs.Projects;

/// <summary>One entry in a project's activity feed (a task action from the audit trail).</summary>
public class ActivityEntryDto
{
    public Guid Id { get; set; }

    /// <summary>Dotted action code, e.g. "task.status.changed".</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Human-readable detail, e.g. "Status: Backlog → Done".</summary>
    public string? Details { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>The task the action was on (null if the id couldn't be parsed).</summary>
    public Guid? TaskId { get; set; }

    public Guid? ActorId { get; set; }

    /// <summary>Actor's display name ("Deleted user" when the account is gone).</summary>
    public string ActorName { get; set; } = string.Empty;

    public string? ActorAvatarUrl { get; set; }
}
