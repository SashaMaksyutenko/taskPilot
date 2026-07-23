using MassTransit;
using Taskpilot.API.Services;

namespace Taskpilot.API.Consumers;

/// <summary>
/// Consumes queued <see cref="NotificationDeliveryMessage"/>s and delivers them over
/// email/Telegram/Viber, off the request thread. The message already carries the recipient's
/// contact snapshot (resolved by the publisher), so delivery needs no database access — web
/// push and quiet hours were handled by the publisher before the message was queued.
/// </summary>
public class NotificationDeliveryConsumer : IConsumer<NotificationDeliveryMessage>
{
    private readonly INotificationDispatcher _dispatcher;
    private readonly ILogger<NotificationDeliveryConsumer> _logger;

    public NotificationDeliveryConsumer(INotificationDispatcher dispatcher, ILogger<NotificationDeliveryConsumer> logger)
    {
        _dispatcher = dispatcher;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<NotificationDeliveryMessage> context)
    {
        var m = context.Message;
        if (m.Recipient is null)
        {
            // Enrichment is always done at publish time; a null snapshot means a malformed or
            // legacy message, which we cannot deliver without re-reading the database.
            _logger.LogWarning("Skipping delivery: message has no recipient snapshot. RecipientId: {RecipientId}", m.RecipientId);
            return;
        }

        _logger.LogInformation("Delivering queued notification. RecipientId: {RecipientId}, Type: {Type}", m.RecipientId, m.Type);
        await _dispatcher.DispatchAsync(m.Recipient, m.Message, m.Link);
    }
}
