namespace Taskpilot.API.Models;

/// <summary>
/// A browser Web Push subscription for a user. One user (one browser/device) has
/// one row; the keys are needed to encrypt payloads to that endpoint.
/// </summary>
public class PushSubscription
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>User the subscription belongs to (foreign key).</summary>
    public Guid UserId { get; set; }

    /// <summary>Navigation to the user.</summary>
    public User User { get; set; } = null!;

    /// <summary>Push service endpoint URL (unique per browser subscription).</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>Client public key (p256dh) used to encrypt the payload.</summary>
    public string P256dh { get; set; } = string.Empty;

    /// <summary>Client auth secret used to encrypt the payload.</summary>
    public string Auth { get; set; } = string.Empty;

    /// <summary>UTC time the subscription was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
