namespace Taskpilot.API.DTOs.Planning;

/// <summary>A prioritized "what to do next" plan across the user's open, assigned tasks.</summary>
public class NextActionsDto
{
    /// <summary>True when the LLM is configured (so the order carries AI reasons).</summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// True when the AI actually ranked the list; false when we fell back to a deterministic order
    /// (no key, or the model returned nothing usable).
    /// </summary>
    public bool RankedByAi { get; set; }

    /// <summary>The tasks to work on, best first.</summary>
    public List<NextActionItemDto> Items { get; set; } = new();
}

/// <summary>One task in the plan, with the signals behind its placement.</summary>
public class NextActionItemDto
{
    public Guid TaskId { get; set; }
    public Guid ProjectId { get; set; }

    /// <summary>Per-project sequential number (shown as e.g. "TP-142").</summary>
    public int Number { get; set; }

    public string Title { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public DateTime? Deadline { get; set; }

    /// <summary>Deadline has passed.</summary>
    public bool IsOverdue { get; set; }

    /// <summary>Waiting on an unfinished dependency — can't be started yet.</summary>
    public bool IsBlocked { get; set; }

    /// <summary>The AI's one-line rationale for this task's placement (null in the fallback order).</summary>
    public string? Reason { get; set; }
}
