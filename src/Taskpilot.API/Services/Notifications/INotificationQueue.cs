
namespace Taskpilot.API.Services;

/// <summary>
/// Publishes notification side-channel delivery onto a message bus. When disabled
/// (no RabbitMQ configured), callers deliver inline instead — check
/// <see cref="IsEnabled"/> first.
/// </summary>
public interface INotificationQueue
{
    /// <summary>True when a message bus is configured and delivery should be queued.</summary>
    bool IsEnabled { get; }

    /// <summary>Publishes a delivery request onto the bus.</summary>
    Task PublishAsync(NotificationDeliveryMessage message);
}
