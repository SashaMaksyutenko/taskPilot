using Taskpilot.API.Common;
using Taskpilot.API.DTOs.Notifications;
using Taskpilot.API.Models;

namespace Taskpilot.API.Services;

/// <summary>
/// Creates and reads in-app notifications. Other services call <see cref="CreateAsync"/>
/// to notify users about events (e.g. a new application).
/// </summary>
public interface INotificationService
{
    /// <summary>Creates a notification for a user (used internally by other services).</summary>
    Task CreateAsync(Guid recipientId, NotificationType type, string message, string? link = null);

    /// <summary>Lists a user's notifications (newest first), optionally only unread ones.</summary>
    Task<Result<List<NotificationDto>>> GetForUserAsync(Guid userId, bool unreadOnly);

    /// <summary>Returns the count of unread notifications for the bell badge.</summary>
    Task<Result<int>> GetUnreadCountAsync(Guid userId);

    /// <summary>Marks one notification as read (only the owner's own notification).</summary>
    Task<Result> MarkReadAsync(Guid userId, Guid notificationId);

    /// <summary>Marks all of a user's notifications as read.</summary>
    Task<Result> MarkAllReadAsync(Guid userId);

    /// <summary>Returns the notification type names the user has opted out of.</summary>
    Task<Result<List<string>>> GetDisabledTypesAsync(Guid userId);

    /// <summary>Replaces the user's opt-out set; returns the applied disabled types.</summary>
    Task<Result<List<string>>> SetDisabledTypesAsync(Guid userId, IEnumerable<string> typeNames);

    /// <summary>Returns the notification types the user muted for email delivery.</summary>
    Task<Result<List<string>>> GetDisabledEmailTypesAsync(Guid userId);

    /// <summary>Replaces the user's email-muted type set; returns the applied disabled types.</summary>
    Task<Result<List<string>>> SetDisabledEmailTypesAsync(Guid userId, IEnumerable<string> typeNames);

    /// <summary>Returns the user's digest email cadence ("Off"/"Daily"/"Weekly").</summary>
    Task<Result<string>> GetDigestFrequencyAsync(Guid userId);

    /// <summary>Sets the user's digest email cadence; returns the applied value.</summary>
    Task<Result<string>> SetDigestFrequencyAsync(Guid userId, string frequency);
}
