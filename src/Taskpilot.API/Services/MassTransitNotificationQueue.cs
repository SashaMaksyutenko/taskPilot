using MassTransit;
using Taskpilot.API.Messages;

namespace Taskpilot.API.Services;

/// <summary>Publishes notification delivery onto RabbitMQ via MassTransit.</summary>
public class MassTransitNotificationQueue : INotificationQueue
{
    private readonly IPublishEndpoint _publish;

    public MassTransitNotificationQueue(IPublishEndpoint publish)
    {
        _publish = publish;
    }

    /// <inheritdoc />
    public bool IsEnabled => true;

    /// <inheritdoc />
    public Task PublishAsync(NotificationDeliveryMessage message) => _publish.Publish(message);
}

/// <summary>
/// No-op queue used when RabbitMQ is not configured. <see cref="IsEnabled"/> is false
/// so callers deliver inline and never publish.
/// </summary>
public class DisabledNotificationQueue : INotificationQueue
{
    /// <inheritdoc />
    public bool IsEnabled => false;

    /// <inheritdoc />
    public Task PublishAsync(NotificationDeliveryMessage message) => Task.CompletedTask;
}
