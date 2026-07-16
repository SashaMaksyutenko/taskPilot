using Taskpilot.API.Common;
using Taskpilot.API.DTOs.Webhooks;

namespace Taskpilot.API.Services;

/// <summary>
/// Manages outgoing webhooks and delivers events to subscribers.
/// </summary>
public interface IWebhookService
{
    Task<Result<WebhookDto>> CreateAsync(Guid ownerId, CreateWebhookDto dto);

    Task<Result<List<WebhookDto>>> GetForUserAsync(Guid ownerId);

    Task<Result> DeleteAsync(Guid ownerId, Guid webhookId);

    /// <summary>
    /// Delivers an event to all active webhooks subscribed to it. Each delivery is a
    /// POST with the JSON payload and an HMAC-SHA256 signature header, retried up to
    /// 3 times, and recorded in the delivery log. Failures are logged, not thrown
    /// (one bad receiver must not break the operation).
    /// </summary>
    Task DispatchAsync(string eventName, object payload);

    /// <summary>Pauses or resumes a webhook (paused ones receive nothing).</summary>
    Task<Result<WebhookDto>> SetActiveAsync(Guid ownerId, Guid webhookId, bool isActive);

    /// <summary>Sends a sample payload to a webhook and returns the delivery outcome.</summary>
    Task<Result<WebhookDeliveryDto>> TestAsync(Guid ownerId, Guid webhookId);

    /// <summary>Lists a webhook's recent deliveries (newest first).</summary>
    Task<Result<List<WebhookDeliveryDto>>> GetDeliveriesAsync(Guid ownerId, Guid webhookId, int limit = 20);
}
