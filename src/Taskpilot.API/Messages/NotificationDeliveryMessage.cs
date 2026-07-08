using Taskpilot.API.Models;

namespace Taskpilot.API.Messages;

/// <summary>
/// Queued request to deliver one notification over the out-of-band side channels
/// (email, Telegram, Viber, web push). Published by the API and handled by the
/// notification consumer when RabbitMQ is enabled.
/// </summary>
public record NotificationDeliveryMessage(Guid RecipientId, NotificationType Type, string Message, string? Link);
