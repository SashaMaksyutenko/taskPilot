namespace Taskpilot.API.DTOs.Projects;

/// <summary>Body for moving only a task's deadline (calendar drag-and-drop).</summary>
public class RescheduleTaskDto
{
    /// <summary>The new deadline (UTC); null clears it.</summary>
    public DateTime? Deadline { get; set; }
}
