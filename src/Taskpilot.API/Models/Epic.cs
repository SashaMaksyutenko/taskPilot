namespace Taskpilot.API.Models;

/// <summary>
/// An epic in a project — a large body of work / theme that groups related tasks, independent
/// of sprints (an epic can span several sprints). Tasks reference their epic via
/// <see cref="ProjectTask.EpicId"/>; a task with no epic is simply ungrouped.
/// </summary>
public class Epic
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    public Project Project { get; set; } = null!;

    public string Title { get; set; } = string.Empty;

    /// <summary>Optional hex colour used to tint the epic's chip on task cards.</summary>
    public string? Color { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
