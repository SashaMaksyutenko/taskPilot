using MassTransit;
using Taskpilot.Contracts;

namespace Taskpilot.NotificationService.Consumers;

/// <summary>
/// Consumes queued <see cref="NotificationDeliveryMessage"/>s published by the API and
/// performs the out-of-band delivery (email, Telegram, Viber, web push).
/// </summary>
/// <remarks>
/// Scaffold stage: this currently only logs receipt, to prove the API → RabbitMQ → service
/// topology end-to-end. The actual delivery logic (today in the API's
/// <c>NotificationDeliveryService</c>) moves here in a follow-up session, once its data
/// dependencies are shared or the message is enriched with the recipient's contact details.
/// </remarks>
public class NotificationDeliveryConsumer : IConsumer<NotificationDeliveryMessage>
{
    private readonly ILogger<NotificationDeliveryConsumer> _logger;

    public NotificationDeliveryConsumer(ILogger<NotificationDeliveryConsumer> logger)
    {
        _logger = logger;
    }

    /// <summary>Handles one queued delivery request.</summary>
    /// <param name="context">The message envelope from MassTransit.</param>
    public Task Consume(ConsumeContext<NotificationDeliveryMessage> context)
    {
        var m = context.Message;
        _logger.LogInformation(
            "Received notification delivery. RecipientId: {RecipientId}, Type: {Type}, Link: {Link}",
            m.RecipientId, m.Type, m.Link);

        // TODO (next session): perform the real delivery here instead of just logging.
        return Task.CompletedTask;
    }
}
