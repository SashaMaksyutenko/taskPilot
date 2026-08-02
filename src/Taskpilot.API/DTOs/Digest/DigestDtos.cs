namespace Taskpilot.API.DTOs.Digest;

/// <summary>A user's week-in-review numbers across the projects they can access.</summary>
public class DigestDto
{
    /// <summary>Start of the 7-day window (UTC).</summary>
    public DateTime WeekStart { get; set; }

    /// <summary>Tasks completed in the last 7 days.</summary>
    public int Completed { get; set; }

    /// <summary>Tasks created in the last 7 days.</summary>
    public int Created { get; set; }

    /// <summary>Unfinished tasks whose deadline has passed.</summary>
    public int Overdue { get; set; }

    /// <summary>Unfinished tasks due within the next 7 days.</summary>
    public int DueSoon { get; set; }

    /// <summary>A few recently completed task titles (for context/summary).</summary>
    public List<string> TopCompleted { get; set; } = new();

    /// <summary>A few overdue task titles.</summary>
    public List<string> TopOverdue { get; set; } = new();

    /// <summary>A few upcoming task titles.</summary>
    public List<string> TopDueSoon { get; set; } = new();
}

/// <summary>An AI-written narrative of the week (empty when the LLM isn't configured).</summary>
public class DigestSummaryDto
{
    /// <summary>False when no LLM is configured — show the numbers without a narrative.</summary>
    public bool Enabled { get; set; }

    public string Summary { get; set; } = string.Empty;
}
