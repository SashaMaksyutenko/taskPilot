using Microsoft.Extensions.Options;

namespace Taskpilot.Integrations;

/// <summary>
/// Default <see cref="INotificationDispatcher"/>: fans a notification out to email, Telegram
/// and Viber. Every channel is best-effort and each sender is a no-op when its provider is
/// not configured, so a missing channel never breaks delivery. This is the exact orchestration
/// that used to live in the API's delivery service, minus the database reads.
/// </summary>
public class NotificationDispatcher : INotificationDispatcher
{
    private readonly IEmailSender _email;
    private readonly ITelegramSender _telegram;
    private readonly IViberSender _viber;
    private readonly EmailOptions _emailOptions;

    public NotificationDispatcher(
        IEmailSender email,
        ITelegramSender telegram,
        IViberSender viber,
        IOptions<EmailOptions> emailOptions)
    {
        _email = email;
        _telegram = telegram;
        _viber = viber;
        _emailOptions = emailOptions.Value;
    }

    /// <inheritdoc />
    public async Task DispatchAsync(NotificationRecipient recipient, string message, string? link)
    {
        // Email is opt-out per notification type; the caller resolved that into EmailMuted.
        if (!recipient.EmailMuted)
            await SendEmailAsync(recipient, message, link);

        await SendTelegramAsync(recipient, message, link);
        await SendViberAsync(recipient, message, link);
    }

    /// <summary>Emails the recipient when email is enabled and they have an address.</summary>
    private async Task SendEmailAsync(NotificationRecipient recipient, string message, string? link)
    {
        if (!_email.IsEnabled || string.IsNullOrWhiteSpace(recipient.Email))
            return;

        var url = AbsoluteUrl(link);
        var html =
            $"<p>Hi {System.Net.WebUtility.HtmlEncode(recipient.Name)},</p>" +
            $"<p>{System.Net.WebUtility.HtmlEncode(message)}</p>" +
            $"<p><a href=\"{url}\">Open in TaskPilot</a></p>";

        await _email.SendAsync(recipient.Email, recipient.Name ?? string.Empty, "TaskPilot notification", html);
    }

    /// <summary>Sends to the recipient's linked Telegram chat, if any.</summary>
    private async Task SendTelegramAsync(NotificationRecipient recipient, string message, string? link)
    {
        if (!_telegram.IsEnabled || string.IsNullOrEmpty(recipient.TelegramChatId))
            return;

        await _telegram.SendMessageAsync(recipient.TelegramChatId, $"{message}\n{AbsoluteUrl(link)}");
    }

    /// <summary>Sends to the recipient's linked Viber, if any.</summary>
    private async Task SendViberAsync(NotificationRecipient recipient, string message, string? link)
    {
        if (!_viber.IsEnabled || string.IsNullOrEmpty(recipient.ViberId))
            return;

        await _viber.SendMessageAsync(recipient.ViberId, $"{message}\n{AbsoluteUrl(link)}");
    }

    // Turns a relative link (e.g. "/projects/{id}") into a clickable absolute URL.
    private string AbsoluteUrl(string? link) =>
        string.IsNullOrEmpty(link)
            ? _emailOptions.FrontendBaseUrl
            : _emailOptions.FrontendBaseUrl.TrimEnd('/') + "/" + link.TrimStart('/');
}
