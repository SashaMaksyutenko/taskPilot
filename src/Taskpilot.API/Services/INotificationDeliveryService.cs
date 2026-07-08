using Taskpilot.API.Models;

namespace Taskpilot.API.Services;

/// <summary>
/// Delivers a notification to the recipient's out-of-band channels (email, Telegram,
/// Viber, web push). Split out of <see cref="NotificationService"/> so the same
/// delivery can run either inline or from a queue consumer.
/// </summary>
public interface INotificationDeliveryService
{
    /// <summary>Sends the notification over every configured side channel (best-effort).</summary>
    Task DeliverAsync(Guid recipientId, NotificationType type, string message, string? link);
}
