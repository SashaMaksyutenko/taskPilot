namespace Taskpilot.API.DTOs.Projects;

/// <summary>Input for updating a task's editable fields.</summary>
public class UpdateTaskDto
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>"Low" | "Medium" | "High".</summary>
    public string? Priority { get; set; }

    public Guid? AssigneeId { get; set; }
    public DateTime? Deadline { get; set; }

    /// <summary>Free-form labels; null leaves the existing tags unchanged.</summary>
    public List<string>? Tags { get; set; }
}
