namespace Taskpilot.Integrations;

/// <summary>
/// Sends one notification to a resolved recipient over email, Telegram and Viber. Takes an
/// already-resolved <see cref="NotificationRecipient"/> so it needs no database — which lets
/// the SAME code run inline inside the API and inside the standalone notification service.
/// Push is deliberately not here (it reads its subscriptions from the database).
/// </summary>
public interface INotificationDispatcher
{
    /// <summary>Delivers the message over every channel the recipient has and that is enabled.</summary>
    Task DispatchAsync(NotificationRecipient recipient, string message, string? link);
}
