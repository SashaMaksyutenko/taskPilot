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
    /// POST with the JSON payload and an HMAC-SHA256 signature header. Failures are
    /// logged, not thrown (one bad receiver must not break the operation).
    /// </summary>
    Task DispatchAsync(string eventName, object payload);
}
