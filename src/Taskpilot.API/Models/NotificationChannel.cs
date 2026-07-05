namespace Taskpilot.API.Models;

/// <summary>Delivery channel a notification preference applies to.</summary>
public enum NotificationChannel
{
    /// <summary>The in-app bell / real-time notification.</summary>
    InApp = 0,

    /// <summary>Email delivery.</summary>
    Email = 1,
}
