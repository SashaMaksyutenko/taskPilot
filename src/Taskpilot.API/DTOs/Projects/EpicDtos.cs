namespace Taskpilot.API.DTOs.Projects;

/// <summary>An epic with its task tallies.</summary>
public class EpicDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Color { get; set; }

    /// <summary>Tasks in the epic.</summary>
    public int TaskCount { get; set; }

    /// <summary>Completed tasks in the epic.</summary>
    public int DoneCount { get; set; }
}

/// <summary>Input for creating/updating an epic.</summary>
public class SaveEpicDto
{
    public string Title { get; set; } = string.Empty;
    public string? Color { get; set; }
}

/// <summary>Input for moving a task into an epic (or ungrouping it).</summary>
public class AssignEpicDto
{
    /// <summary>The target epic, or null to remove the task from its epic.</summary>
    public Guid? EpicId { get; set; }
}
