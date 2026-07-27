namespace Taskpilot.API.Models;

/// <summary>
/// A 1–5 star peer review one user leaves about another, scoped to the <see cref="Context"/>
/// they interacted in (a completed marketplace task, a shared project, or a forum thread).
/// Rater/ratee are stored by id (no navigation) to keep the model simple and avoid extra
/// cascade paths to Users.
/// </summary>
public class Review
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>What relationship the review was left in.</summary>
    public ReviewContext Context { get; set; } = ReviewContext.Marketplace;

    /// <summary>
    /// The entity the review is scoped to — the project id for a Project review, the topic id
    /// for a Forum review, or the marketplace task id for a Marketplace review. Null only for
    /// legacy marketplace rows that predate this column (backfilled by migration).
    /// </summary>
    public Guid? ContextId { get; set; }

    /// <summary>The completed marketplace task this review is about, when Context = Marketplace.</summary>
    public Guid? MarketplaceTaskId { get; set; }

    /// <summary>Navigation to the task (marketplace reviews only).</summary>
    public MarketplaceTask? MarketplaceTask { get; set; }

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
