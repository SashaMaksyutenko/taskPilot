using Taskpilot.API.Common;

namespace Taskpilot.API.Services;

/// <summary>
/// Manages browser Web Push subscriptions and sends push notifications. A no-op
/// for sending when VAPID keys are not configured.
/// </summary>
public interface IPushService
{
    /// <summary>True when VAPID keys are configured (push can be sent).</summary>
    bool IsEnabled { get; }

    /// <summary>The public VAPID key the browser needs to subscribe; empty when disabled.</summary>
    string PublicKey { get; }

    /// <summary>Stores (or refreshes) a browser subscription for the user.</summary>
    Task<Result> SubscribeAsync(Guid userId, string endpoint, string p256dh, string auth);

    /// <summary>Removes a browser subscription by its endpoint.</summary>
    Task<Result> UnsubscribeAsync(Guid userId, string endpoint);

    /// <summary>Sends a push notification to all of the user's subscriptions (best-effort).</summary>
    Task SendToUserAsync(Guid userId, string title, string body, string? url);
}
