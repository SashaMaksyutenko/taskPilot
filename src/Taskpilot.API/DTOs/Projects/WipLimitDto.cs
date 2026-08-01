namespace Taskpilot.API.DTOs.Projects;

/// <summary>A WIP limit on one Kanban column of a project.</summary>
public class WipLimitDto
{
    /// <summary>The column the limit applies to ("Backlog" | "InProgress" | "Review" | "Done").</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Maximum tasks allowed in the column.</summary>
    public int MaxTasks { get; set; }
}

/// <summary>Sets (or clears) a column's WIP limit.</summary>
public class SetWipLimitDto
{
    public string Status { get; set; } = string.Empty;

    /// <summary>The limit to set; null or ≤ 0 removes the limit on that column.</summary>
    public int? MaxTasks { get; set; }
}
