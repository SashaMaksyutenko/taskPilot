namespace Taskpilot.API.Models;

/// <summary>
/// One recorded attempt-set at delivering an event to a webhook. Written after the
/// final attempt so the owner can see what happened (status code, error, retries).
/// </summary>
public class WebhookDelivery
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Webhook the event was delivered to (foreign key).</summary>
    public Guid WebhookId { get; set; }

    /// <summary>Navigation to the webhook.</summary>
    public Webhook Webhook { get; set; } = null!;

    /// <summary>Event name that was delivered (e.g. "task.completed").</summary>
    public string Event { get; set; } = string.Empty;

    /// <summary>True when the receiver answered with a 2xx status.</summary>
    public bool Success { get; set; }

    /// <summary>HTTP status the receiver returned; null when the request never completed.</summary>
    public int? StatusCode { get; set; }

    /// <summary>Error message when the request failed outright; null on an HTTP response.</summary>
    public string? Error { get; set; }

    /// <summary>How many attempts were made (1–3).</summary>
    public int Attempts { get; set; }

    /// <summary>UTC time the delivery finished.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
