namespace Taskpilot.API.DTOs.Webhooks;

/// <summary>A recorded webhook delivery, shaped for the owner's delivery log.</summary>
public class WebhookDeliveryDto
{
    public Guid Id { get; set; }
    public string Event { get; set; } = string.Empty;
    public bool Success { get; set; }

    /// <summary>HTTP status the receiver returned; null when the request never completed.</summary>
    public int? StatusCode { get; set; }

    /// <summary>Error message when the request failed outright.</summary>
    public string? Error { get; set; }

    /// <summary>How many attempts were made (1–3).</summary>
    public int Attempts { get; set; }

    public DateTime CreatedAt { get; set; }
}

/// <summary>Body for pausing/resuming a webhook.</summary>
public class SetWebhookActiveDto
{
    public bool IsActive { get; set; }
}
