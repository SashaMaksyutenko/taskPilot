namespace Taskpilot.API.Configuration;

/// <summary>
/// RabbitMQ settings, bound from the "RabbitMq" configuration section. When no
/// connection string is set, the message bus is disabled and notification
/// side-channel delivery runs inline (in-process) instead of via a queue.
/// </summary>
public class RabbitMqOptions
{
    /// <summary>AMQP connection string, e.g. amqp://guest:guest@localhost:5672. Empty = disabled.</summary>
    public string Connection { get; set; } = string.Empty;

    /// <summary>True only when a connection string is configured.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(Connection);
}
