namespace Taskpilot.API.Models;

/// <summary>
/// A 1–5 star review left by one party of a completed marketplace task about the
/// other party. Rater/ratee are stored by id (no navigation) to keep the model
/// simple and avoid extra cascade paths to Users.
/// </summary>
public class Review
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>The completed task this review is about (foreign key).</summary>
    public Guid MarketplaceTaskId { get; set; }

    /// <summary>Navigation to the task.</summary>
    public MarketplaceTask MarketplaceTask { get; set; } = null!;

    /// <summary>User who left the review.</summary>
    public Guid RaterId { get; set; }

    /// <summary>User the review is about.</summary>
    public Guid RateeId { get; set; }

    /// <summary>Score from 1 to 5.</summary>
    public int Stars { get; set; }

    /// <summary>Optional written feedback.</summary>
    public string? Comment { get; set; }

    /// <summary>UTC time the review was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
