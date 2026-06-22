namespace Taskpilot.API.Models;

/// <summary>
/// An outgoing webhook: when the chosen <see cref="Event"/> happens, TaskPilot sends
/// an HTTP POST to <see cref="Url"/> with an HMAC-SHA256 signature so the receiver can
/// verify it. Owned by the user who registered it.
/// </summary>
public class Webhook
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>User who owns the webhook (foreign key).</summary>
    public Guid OwnerId { get; set; }

    /// <summary>Navigation to the owner.</summary>
    public User Owner { get; set; } = null!;

    /// <summary>Destination URL that receives the POST.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Event name to listen for, e.g. "task.completed".</summary>
    public string Event { get; set; } = string.Empty;

    /// <summary>Secret used to sign the payload (HMAC-SHA256).</summary>
    public string Secret { get; set; } = string.Empty;

    /// <summary>Whether the webhook is active (paused webhooks are not delivered).</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>UTC time the webhook was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
