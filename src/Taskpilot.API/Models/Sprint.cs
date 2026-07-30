namespace Taskpilot.API.Models;

/// <summary>
/// A sprint / iteration in a project — a time-boxed set of tasks. Tasks reference their sprint
/// via <see cref="ProjectTask.SprintId"/>; a task with no sprint sits in the backlog.
/// </summary>
public class Sprint
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    public Project Project { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    /// <summary>Optional sprint goal / summary.</summary>
    public string? Goal { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public SprintStatus Status { get; set; } = SprintStatus.Planned;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
