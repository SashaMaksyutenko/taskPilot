namespace Taskpilot.Contracts;

/// <summary>
/// Queued request to deliver one notification over the out-of-band side channels
/// (email, Telegram, Viber, web push). Published by the API and handled by the
/// notification consumer when a message bus (RabbitMQ) is configured.
/// </summary>
/// <param name="RecipientId">User to deliver to (kept for logging/traceability).</param>
/// <param name="Type">Notification category (kept for logging).</param>
/// <param name="Message">The human-readable notification text.</param>
/// <param name="Link">Optional in-app link the notification points at.</param>
/// <param name="Recipient">
/// The recipient's contact details and email-mute flag, resolved by the publisher so the
/// consumer can deliver over email/Telegram/Viber without any database access. Null on
/// legacy messages that predate enrichment.
/// </param>
public record NotificationDeliveryMessage(
    Guid RecipientId,
    NotificationType Type,
    string Message,
    string? Link,
    NotificationRecipient? Recipient = null);
