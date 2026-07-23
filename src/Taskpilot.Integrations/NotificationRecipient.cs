namespace Taskpilot.Integrations;

/// <summary>
/// A resolved snapshot of everything the dispatcher needs to reach one recipient over the
/// out-of-band channels — pulled from the database by the caller, so the dispatcher itself
/// (and the notification service that runs it) needs no data access.
/// </summary>
/// <param name="Email">Recipient email, or null/empty when they have none.</param>
/// <param name="Name">Display name, used in the email greeting.</param>
/// <param name="TelegramChatId">Linked Telegram chat id, or null when not linked.</param>
/// <param name="ViberId">Linked Viber id, or null when not linked.</param>
/// <param name="EmailMuted">True when the recipient opted out of email for this notification's type.</param>
public record NotificationRecipient(
    string? Email,
    string? Name,
    string? TelegramChatId,
    string? ViberId,
    bool EmailMuted);
