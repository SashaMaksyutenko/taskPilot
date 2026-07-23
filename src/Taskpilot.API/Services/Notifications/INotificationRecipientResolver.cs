namespace Taskpilot.API.Services;

/// <summary>
/// Resolves the database-backed <see cref="Taskpilot.Contracts.NotificationRecipient"/> snapshot
/// (contact details + email-mute flag) for a recipient. Used both to enrich a queued delivery
/// message at publish time and by the inline delivery path, so the resolution lives in one place.
/// </summary>
public interface INotificationRecipientResolver
{
    /// <summary>Loads the recipient's contact snapshot for a notification of the given type.</summary>
    Task<NotificationRecipient> ResolveAsync(Guid recipientId, NotificationType type);
}
