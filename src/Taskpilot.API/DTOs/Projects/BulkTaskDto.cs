namespace Taskpilot.API.DTOs.Projects;

/// <summary>Input for a bulk status change over several tasks.</summary>
public class BulkStatusDto
{
    /// <summary>Ids of the tasks to update.</summary>
    public List<Guid> TaskIds { get; set; } = new();

    /// <summary>Target status (Backlog / InProgress / Review / Done).</summary>
    public string Status { get; set; } = string.Empty;
}

/// <summary>Input for a bulk delete over several tasks.</summary>
public class BulkDeleteDto
{
    /// <summary>Ids of the tasks to delete.</summary>
    public List<Guid> TaskIds { get; set; } = new();
}
