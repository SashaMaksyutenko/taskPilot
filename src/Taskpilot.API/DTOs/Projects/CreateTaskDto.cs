namespace Taskpilot.API.DTOs.Projects;

/// <summary>Input for creating a task in a project.</summary>
public class CreateTaskDto
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>"Low" | "Medium" | "High". Defaults to Medium when empty.</summary>
    public string? Priority { get; set; }

    /// <summary>Optional user to assign the task to.</summary>
    public Guid? AssigneeId { get; set; }

    /// <summary>Optional deadline (UTC).</summary>
    public DateTime? Deadline { get; set; }

    /// <summary>Optional parent task id for subtasks.</summary>
    public Guid? ParentTaskId { get; set; }

    /// <summary>Optional effort estimate in story points.</summary>
    public int? Estimate { get; set; }

    /// <summary>Optional recurrence: "None", "Daily", "Weekly" or "Monthly".</summary>
    public string? Recurrence { get; set; }

    /// <summary>Repeat every N units (defaults to 1). Only used when Recurrence isn't None.</summary>
    public int? RecurrenceInterval { get; set; }

    /// <summary>Optional free-form labels.</summary>
    public List<string>? Tags { get; set; }
}
