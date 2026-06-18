namespace Taskpilot.API.Models;

/// <summary>
/// A public task posted to the marketplace. Developers apply, the poster accepts one,
/// and the task moves through its lifecycle until completed.
/// </summary>
public class MarketplaceTask
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Short title of the task.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Full description of the work.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Offered budget.</summary>
    public decimal Budget { get; set; }

    /// <summary>Comma-separated list of required skills (kept simple for now).</summary>
    public string? RequiredSkills { get; set; }

    /// <summary>Optional deadline (UTC).</summary>
    public DateTime? Deadline { get; set; }

    /// <summary>Current lifecycle status.</summary>
    public MarketplaceTaskStatus Status { get; set; } = MarketplaceTaskStatus.Open;

    /// <summary>User who posted the task (foreign key).</summary>
    public Guid PosterId { get; set; }

    /// <summary>Navigation to the poster.</summary>
    public User Poster { get; set; } = null!;

    /// <summary>Accepted applicant working on the task; null until one is accepted.</summary>
    public Guid? AssigneeId { get; set; }

    /// <summary>Navigation to the assignee (if any).</summary>
    public User? Assignee { get; set; }

    /// <summary>UTC time the task was posted.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Applications submitted for this task.</summary>
    public ICollection<TaskApplication> Applications { get; set; } = new List<TaskApplication>();
}
