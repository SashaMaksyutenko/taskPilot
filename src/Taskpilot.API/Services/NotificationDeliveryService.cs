using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Taskpilot.API.Common;
using Taskpilot.API.Configuration;
using Taskpilot.API.Data;
using Taskpilot.API.Models;

namespace Taskpilot.API.Services;

/// <summary>
/// Sends a notification over the out-of-band channels: email, Telegram, Viber and
/// web push. Every channel is best-effort — a failure is logged and swallowed so it
/// never breaks the caller. Email is skipped when muted for the (type, Email) pair.
/// </summary>
public class NotificationDeliveryService : INotificationDeliveryService
{
    private readonly TaskpilotDbContext _context;
    private readonly IEmailSender _email;
    private readonly ITelegramSender _telegram;
    private readonly IViberSender _viber;
    private readonly IPushService _push;
    private readonly EmailOptions _emailOptions;

    public NotificationDeliveryService(
        TaskpilotDbContext context,
        IEmailSender email,
        ITelegramSender telegram,
        IViberSender viber,
        IPushService push,
        IOptions<EmailOptions> emailOptions)
    {
        _context = context;
        _email = email;
        _telegram = telegram;
        _viber = viber;
        _push = push;
        _emailOptions = emailOptions.Value;
    }

    /// <inheritdoc />
    public async Task DeliverAsync(Guid recipientId, NotificationType type, string message, string? link)
    {
        // Quiet hours hold back every out-of-band channel. The in-app notification was
        // already stored by the caller, so nothing is lost — the bell just waits.
        if (await IsInQuietHoursAsync(recipientId))
            return;

        // Email is opted out independently per (type, Email channel).
        var emailMuted = await _context.NotificationPreferences
            .AnyAsync(p => p.UserId == recipientId && p.Type == type && p.Channel == NotificationChannel.Email);

        if (!emailMuted)
            await SendEmailAsync(recipientId, message, link);

        await SendTelegramAsync(recipientId, message, link);
        await SendViberAsync(recipientId, message, link);

        var pushUrl = AbsoluteUrl(link);
        await _push.SendToUserAsync(recipientId, "TaskPilot", message, pushUrl);
    }

    /// <summary>True when the recipient has quiet hours on and is inside their window.</summary>
    private async Task<bool> IsInQuietHoursAsync(Guid recipientId)
    {
        var settings = await _context.Users
            .Where(u => u.Id == recipientId)
            .Select(u => new { u.QuietHoursEnabled, u.QuietHoursStart, u.QuietHoursEnd, u.TimeZoneId })
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (settings is null || !settings.QuietHoursEnabled)
            return false;

        return QuietHours.IsQuiet(
            settings.QuietHoursStart,
            settings.QuietHoursEnd,
            settings.TimeZoneId,
            DateTime.UtcNow);
    }

    /// <summary>Sends the notification to the recipient's linked Telegram chat, if any.</summary>
    private async Task SendTelegramAsync(Guid recipientId, string message, string? link)
    {
        if (!_telegram.IsEnabled)
            return;

        var chatId = await _context.Users
            .Where(u => u.Id == recipientId)
            .Select(u => u.TelegramChatId)
            .FirstOrDefaultAsync();
        if (string.IsNullOrEmpty(chatId))
            return;

        await _telegram.SendMessageAsync(chatId, $"{message}\n{AbsoluteUrl(link)}");
    }

    /// <summary>Sends the notification to the recipient's linked Viber, if any.</summary>
    private async Task SendViberAsync(Guid recipientId, string message, string? link)
    {
        if (!_viber.IsEnabled)
            return;

        var viberId = await _context.Users
            .Where(u => u.Id == recipientId)
            .Select(u => u.ViberId)
            .FirstOrDefaultAsync();
        if (string.IsNullOrEmpty(viberId))
            return;

        await _viber.SendMessageAsync(viberId, $"{message}\n{AbsoluteUrl(link)}");
    }

    /// <summary>Emails the notification to the recipient when email delivery is enabled.</summary>
    private async Task SendEmailAsync(Guid recipientId, string message, string? link)
    {
        if (!_email.IsEnabled)
            return;

        var recipient = await _context.Users
            .Where(u => u.Id == recipientId)
            .Select(u => new { u.Email, u.Name })
            .AsNoTracking()
            .FirstOrDefaultAsync();
        if (recipient is null || string.IsNullOrWhiteSpace(recipient.Email))
            return;

        var url = AbsoluteUrl(link);
        var html =
            $"<p>Hi {System.Net.WebUtility.HtmlEncode(recipient.Name)},</p>" +
            $"<p>{System.Net.WebUtility.HtmlEncode(message)}</p>" +
            $"<p><a href=\"{url}\">Open in TaskPilot</a></p>";

        await _email.SendAsync(recipient.Email, recipient.Name, "TaskPilot notification", html);
    }

    // Turns a relative link (e.g. "/projects/{id}") into a clickable absolute URL.
    private string AbsoluteUrl(string? link) =>
        string.IsNullOrEmpty(link)
            ? _emailOptions.FrontendBaseUrl
            : _emailOptions.FrontendBaseUrl.TrimEnd('/') + "/" + link.TrimStart('/');
}
