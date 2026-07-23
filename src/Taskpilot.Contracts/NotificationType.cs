namespace Taskpilot.Contracts;

/// <summary>
/// Category of a notification (used for icons/filtering on the client, and to route
/// side-channel delivery). Shared between the API and the notification service, so a
/// queued <see cref="NotificationDeliveryMessage"/> carries a type both sides understand.
/// </summary>
public enum NotificationType
{
    General = 0,
    Marketplace = 1,
    Forum = 2,
    Chat = 3,
    Task = 4,
    Moderation = 5
}
