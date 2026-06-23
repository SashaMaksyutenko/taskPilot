namespace Taskpilot.API.DTOs.Projects;

/// <summary>A project as returned to clients.</summary>
public class ProjectDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Color { get; set; }
    public Guid OwnerId { get; set; }
    public string OwnerName { get; set; } = string.Empty;
    public int TaskCount { get; set; }

    /// <summary>Number of tasks in the Done status (for the progress bar).</summary>
    public int CompletedTaskCount { get; set; }

    public bool IsArchived { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ArchivedAt { get; set; }
}
