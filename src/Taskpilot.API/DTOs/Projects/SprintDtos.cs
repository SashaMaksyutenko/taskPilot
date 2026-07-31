namespace Taskpilot.API.DTOs.Projects;

/// <summary>Input for creating or updating a sprint.</summary>
public class SaveSprintDto
{
    public string Name { get; set; } = string.Empty;
    public string? Goal { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    /// <summary>"Planned" | "Active" | "Completed". Ignored on create (always starts Planned).</summary>
    public string? Status { get; set; }
}

/// <summary>A sprint with its task tallies (mirrors the sprint list row).</summary>
public class SprintDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Goal { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string Status { get; set; } = "Planned";

    /// <summary>How many tasks are in the sprint, and how many of those are done.</summary>
    public int TaskCount { get; set; }
    public int DoneCount { get; set; }

    /// <summary>Story points committed to the sprint, and points completed (its velocity contribution).</summary>
    public int PlannedPoints { get; set; }
    public int CompletedPoints { get; set; }
}

/// <summary>Input for moving a task into a sprint (null clears it back to the backlog).</summary>
public class AssignSprintDto
{
    public Guid? SprintId { get; set; }
}
