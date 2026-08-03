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

/// <summary>Input for a bulk assignee change over several tasks.</summary>
public class BulkAssignDto
{
    /// <summary>Ids of the tasks to reassign.</summary>
    public List<Guid> TaskIds { get; set; } = new();

    /// <summary>New assignee, or null to unassign.</summary>
    public Guid? AssigneeId { get; set; }
}

/// <summary>Input for a bulk priority change over several tasks.</summary>
public class BulkPriorityDto
{
    /// <summary>Ids of the tasks to update.</summary>
    public List<Guid> TaskIds { get; set; } = new();

    /// <summary>Target priority (Low / Medium / High).</summary>
    public string Priority { get; set; } = string.Empty;
}
