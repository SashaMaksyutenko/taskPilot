namespace Taskpilot.API.Models;

/// <summary>
/// A work-in-progress (WIP) limit on one Kanban column of a project: at most
/// <see cref="MaxTasks"/> tasks may sit in the <see cref="Status"/> column at once.
/// Limits are opt-in per column; when set, moving a task into a full column is rejected.
/// </summary>
public class ProjectWipLimit
{
    public Guid Id { get; set; }

    /// <summary>The project this limit belongs to (foreign key).</summary>
    public Guid ProjectId { get; set; }

    /// <summary>Navigation to the project.</summary>
    public Project Project { get; set; } = null!;

    /// <summary>The Kanban column (task status) the limit applies to.</summary>
    public ProjectTaskStatus Status { get; set; }

    /// <summary>Maximum number of tasks allowed in the column (always ≥ 1).</summary>
    public int MaxTasks { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
