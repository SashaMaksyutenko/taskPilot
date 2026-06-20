namespace Taskpilot.API.DTOs.Projects;

/// <summary>Input for moving a task to another Kanban column.</summary>
public class ChangeStatusDto
{
    /// <summary>"Backlog" | "InProgress" | "Review" | "Done".</summary>
    public string Status { get; set; } = string.Empty;
}
