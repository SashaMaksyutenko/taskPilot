namespace Taskpilot.API.DTOs.Projects;

/// <summary>Created vs completed task counts for one calendar week.</summary>
public class WeekBucketDto
{
    public DateTime WeekStart { get; set; }
    public int Created { get; set; }
    public int Completed { get; set; }
}

/// <summary>How many open and done tasks a member carries.</summary>
public class AssigneeLoadDto
{
    public string Name { get; set; } = string.Empty;
    public int Open { get; set; }
    public int Done { get; set; }
}

/// <summary>Aggregate delivery metrics for a project board.</summary>
public class ProjectAnalyticsDto
{
    public int TotalTasks { get; set; }

    /// <summary>Current task count per Kanban status (all four columns always present).</summary>
    public Dictionary<string, int> ByStatus { get; set; } = new();

    /// <summary>Current task count per priority (Low/Medium/High).</summary>
    public Dictionary<string, int> ByPriority { get; set; } = new();

    /// <summary>Created vs completed counts for the last 8 weeks (oldest first) — the burn-up trend.</summary>
    public List<WeekBucketDto> Weeks { get; set; } = new();

    /// <summary>Average days from creation to completion for finished tasks (null when none are done).</summary>
    public double? AvgCycleTimeDays { get; set; }

    /// <summary>Tasks completed in the current / previous calendar week (throughput).</summary>
    public int ThroughputThisWeek { get; set; }
    public int ThroughputPrevWeek { get; set; }

    /// <summary>Open/done load per assignee (unassigned grouped as "Unassigned"), busiest first.</summary>
    public List<AssigneeLoadDto> ByAssignee { get; set; } = new();
}
