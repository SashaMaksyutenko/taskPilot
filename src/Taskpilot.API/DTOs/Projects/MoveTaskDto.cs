namespace Taskpilot.API.DTOs.Projects;

/// <summary>Input for moving a task to another project.</summary>
public class MoveTaskDto
{
    /// <summary>Id of the destination project.</summary>
    public Guid ProjectId { get; set; }
}
