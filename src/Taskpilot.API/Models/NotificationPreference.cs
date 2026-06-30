namespace Taskpilot.API.Models;

/// <summary>
/// A per-user opt-out for a notification type. A row exists only when the user has
/// disabled that type; the absence of a row means the type is enabled (the default).
/// </summary>
public class NotificationPreference
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>User the preference belongs to (foreign key).</summary>
    public Guid UserId { get; set; }

    /// <summary>Navigation to the user.</summary>
    public User User { get; set; } = null!;

    /// <summary>The notification type that is muted for this user.</summary>
    public NotificationType Type { get; set; }
}
