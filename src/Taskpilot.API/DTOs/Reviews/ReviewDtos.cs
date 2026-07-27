namespace Taskpilot.API.DTOs.Reviews;

/// <summary>Input for leaving a peer review about another user within a context (project, etc.).</summary>
public class LeaveReviewDto
{
    /// <summary>The user being reviewed.</summary>
    public Guid RateeId { get; set; }

    /// <summary>Score from 1 to 5.</summary>
    public int Stars { get; set; }

    /// <summary>Optional written feedback.</summary>
    public string? Comment { get; set; }
}

/// <summary>A review a user has received, with the context it was left in resolved for display.</summary>
public class UserReviewDto
{
    public Guid Id { get; set; }

    /// <summary>"Marketplace", "Project" or "Forum".</summary>
    public string Context { get; set; } = string.Empty;

    /// <summary>Id of the project/topic/task the review was scoped to (may be null for legacy rows).</summary>
    public Guid? ContextId { get; set; }

    /// <summary>Human-readable name of that context entity (project name, task/topic title).</summary>
    public string? ContextLabel { get; set; }

    /// <summary>In-app route to open the context entity.</summary>
    public string? ContextLink { get; set; }

    public Guid RaterId { get; set; }
    public string RaterName { get; set; } = string.Empty;
    public string? RaterAvatarUrl { get; set; }
    public int Stars { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
}
