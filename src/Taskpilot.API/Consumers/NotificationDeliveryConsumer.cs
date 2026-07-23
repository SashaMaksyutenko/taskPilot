using MassTransit;
using Taskpilot.API.Services;

namespace Taskpilot.API.Consumers;

/// <summary>
/// Consumes queued <see cref="NotificationDeliveryMessage"/>s and performs the
/// out-of-band delivery (email, Telegram, Viber, push) off the request thread.
/// </summary>
public class NotificationDeliveryConsumer : IConsumer<NotificationDeliveryMessage>
{
    private readonly INotificationDeliveryService _delivery;
    private readonly ILogger<NotificationDeliveryConsumer> _logger;

    public NotificationDeliveryConsumer(INotificationDeliveryService delivery, ILogger<NotificationDeliveryConsumer> logger)
    {
        _delivery = delivery;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<NotificationDeliveryMessage> context)
    {
        var m = context.Message;
        _logger.LogInformation("Delivering queued notification. RecipientId: {RecipientId}, Type: {Type}", m.RecipientId, m.Type);
        await _delivery.DeliverAsync(m.RecipientId, m.Type, m.Message, m.Link);
    }
}
