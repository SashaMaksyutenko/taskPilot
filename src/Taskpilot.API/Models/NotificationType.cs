namespace Taskpilot.API.Models;

/// <summary>Category of an in-app notification (used for icons/filtering on the client).</summary>
public enum NotificationType
{
    General = 0,
    Marketplace = 1,
    Forum = 2,
    Chat = 3,
    Task = 4,
    Moderation = 5
}
