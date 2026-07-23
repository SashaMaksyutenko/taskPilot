namespace Taskpilot.Contracts;

/// <summary>
/// Queued request to deliver one notification over the out-of-band side channels
/// (email, Telegram, Viber, web push). Published by the API and handled by the
/// notification consumer when a message bus (RabbitMQ) is configured.
/// </summary>
/// <param name="RecipientId">User to deliver to; the consumer looks up their contact details and preferences.</param>
/// <param name="Type">Notification category, used to honour the recipient's per-type channel preferences.</param>
/// <param name="Message">The human-readable notification text.</param>
/// <param name="Link">Optional in-app link the notification points at.</param>
public record NotificationDeliveryMessage(Guid RecipientId, NotificationType Type, string Message, string? Link);
