namespace Taskpilot.API.Models;

/// <summary>
/// One immutable line in a user's reputation history ("ledger"). Written when a
/// point-affecting event happens (task finished on time/late, marketplace task
/// approved, forum solution accepted, warning issued). The profile's total score
/// stays derived; this ledger is the persisted, human-readable history of changes.
/// </summary>
public class ReputationEntry
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>User whose reputation this entry belongs to (foreign key).</summary>
    public Guid UserId { get; set; }

    /// <summary>Navigation to the user.</summary>
    public User User { get; set; } = null!;

    /// <summary>Signed points change (e.g. +15, −10).</summary>
    public int Delta { get; set; }

    /// <summary>Why the change happened.</summary>
    public ReputationReason Reason { get; set; }

    /// <summary>Human-readable description shown in the history (e.g. the task title).</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Optional id of the entity that triggered the entry (task, reply, warning).
    /// Used to keep the ledger idempotent — one entry per source event.
    /// </summary>
    public Guid? RelatedEntityId { get; set; }

    /// <summary>UTC time the entry was recorded.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
