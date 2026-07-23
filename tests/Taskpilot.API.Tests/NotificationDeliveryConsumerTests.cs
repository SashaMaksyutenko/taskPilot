using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Taskpilot.API.Consumers;
using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>
/// Tests the API's in-process <see cref="NotificationDeliveryConsumer"/>: it delivers straight
/// from the message's recipient snapshot through the dispatcher — no database, no re-resolving.
/// </summary>
public class NotificationDeliveryConsumerTests
{
    private static ConsumeContext<NotificationDeliveryMessage> ContextFor(NotificationDeliveryMessage message)
    {
        var ctx = new Mock<ConsumeContext<NotificationDeliveryMessage>>();
        ctx.SetupGet(c => c.Message).Returns(message);
        return ctx.Object;
    }

    [Fact]
    public async Task Consume_DispatchesUsingTheMessageSnapshot()
    {
        var dispatcher = new Mock<INotificationDispatcher>();
        var consumer = new NotificationDeliveryConsumer(dispatcher.Object, NullLogger<NotificationDeliveryConsumer>.Instance);
        var recipient = new NotificationRecipient("d@example.com", "Dana", "tg-1", null, EmailMuted: false);
        var message = new NotificationDeliveryMessage(Guid.NewGuid(), NotificationType.Task, "Hi", "/projects/1", recipient);

        await consumer.Consume(ContextFor(message));

        dispatcher.Verify(d => d.DispatchAsync(recipient, "Hi", "/projects/1"), Times.Once);
    }

    [Fact]
    public async Task Consume_WithoutASnapshot_DeliversNothing()
    {
        // A message with no snapshot can't be delivered without the database, so it is skipped.
        var dispatcher = new Mock<INotificationDispatcher>();
        var consumer = new NotificationDeliveryConsumer(dispatcher.Object, NullLogger<NotificationDeliveryConsumer>.Instance);
        var message = new NotificationDeliveryMessage(Guid.NewGuid(), NotificationType.Task, "Hi", null, Recipient: null);

        await consumer.Consume(ContextFor(message));

        dispatcher.Verify(d => d.DispatchAsync(It.IsAny<NotificationRecipient>(), It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
    }
}
