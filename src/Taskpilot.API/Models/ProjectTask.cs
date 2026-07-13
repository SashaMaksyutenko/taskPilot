namespace Taskpilot.API.Models;

/// <summary>
/// A task within a project. Has a Kanban status, priority, optional assignee and
/// deadline, and can be a subtask of another task via <see cref="ParentTaskId"/>.
/// Named "ProjectTask" to avoid clashing with System.Threading.Tasks.Task and the
/// marketplace's <see cref="MarketplaceTask"/>.
/// </summary>
public class ProjectTask
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Project the task belongs to (foreign key).</summary>
    public Guid ProjectId { get; set; }

    /// <summary>Navigation to the project.</summary>
    public Project Project { get; set; } = null!;

    /// <summary>Task title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Optional details.</summary>
    public string? Description { get; set; }

    /// <summary>Kanban status.</summary>
    public ProjectTaskStatus Status { get; set; } = ProjectTaskStatus.Backlog;

    /// <summary>Priority.</summary>
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;

    /// <summary>Assigned user; null if unassigned.</summary>
    public Guid? AssigneeId { get; set; }

    /// <summary>Navigation to the assignee.</summary>
    public User? Assignee { get; set; }

    /// <summary>User who created the task (foreign key).</summary>
    public Guid CreatorId { get; set; }

    /// <summary>Navigation to the creator.</summary>
    public User Creator { get; set; } = null!;

    /// <summary>Optional parent task for subtasks; null for a top-level task.</summary>
    public Guid? ParentTaskId { get; set; }

    /// <summary>Navigation to the parent task.</summary>
    public ProjectTask? ParentTask { get; set; }

    /// <summary>Optional deadline (UTC).</summary>
    public DateTime? Deadline { get; set; }

    /// <summary>UTC time the task was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>UTC time the task was last updated; null if never.</summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>UTC time the task was marked Done; null otherwise.</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// UTC time the overdue notification/webhook was emitted for this task; null until
    /// then. Prevents the background overdue check from notifying repeatedly.
    /// </summary>
    public DateTime? OverdueNotifiedAt { get; set; }

    /// <summary>
    /// UTC time this task was escalated (still overdue past the escalation threshold);
    /// null until then. Prevents the escalation from firing repeatedly.
    /// </summary>
    public DateTime? EscalatedAt { get; set; }

    /// <summary>Free-form labels attached to the task (stored as a Postgres text[]).</summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>Total tracked time in seconds (accumulated from completed timer runs).</summary>
    public int TimeSpentSeconds { get; set; }

    /// <summary>When a timer is currently running, the UTC time it started; null when stopped.</summary>
    public DateTime? TimerStartedAt { get; set; }
}
