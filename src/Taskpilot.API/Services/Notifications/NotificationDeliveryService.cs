using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Taskpilot.API.Common;
using Taskpilot.API.Data;
using Taskpilot.API.Models;

namespace Taskpilot.API.Services;

/// <summary>
/// Delivers a notification over the out-of-band channels for the INLINE path (no message
/// broker configured). Reads the recipient's contact details and preferences from the
/// database, then hands email/Telegram/Viber to the shared <see cref="INotificationDispatcher"/>
/// (the same code the notification service runs) and sends web push itself, since push reads
/// its subscriptions from the database.
/// </summary>
public class NotificationDeliveryService : INotificationDeliveryService
{
    private readonly TaskpilotDbContext _context;
    private readonly INotificationRecipientResolver _resolver;
    private readonly INotificationDispatcher _dispatcher;
    private readonly IPushService _push;
    private readonly EmailOptions _emailOptions;

    public NotificationDeliveryService(
        TaskpilotDbContext context,
        INotificationRecipientResolver resolver,
        INotificationDispatcher dispatcher,
        IPushService push,
        IOptions<EmailOptions> emailOptions)
    {
        _context = context;
        _resolver = resolver;
        _dispatcher = dispatcher;
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

        // Resolve everything the dispatcher needs, then hand off the email/Telegram/Viber
        // fan-out to the shared dispatcher (the same resolver runs when enriching a queued
        // message, so inline and queued delivery agree on the recipient snapshot).
        var recipient = await _resolver.ResolveAsync(recipientId, type);
        await _dispatcher.DispatchAsync(recipient, message, link);

        // Push stays here: it looks up the recipient's subscriptions in the database.
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

    // Turns a relative link (e.g. "/projects/{id}") into a clickable absolute URL (for push).
    private string AbsoluteUrl(string? link) =>
        string.IsNullOrEmpty(link)
            ? _emailOptions.FrontendBaseUrl
            : _emailOptions.FrontendBaseUrl.TrimEnd('/') + "/" + link.TrimStart('/');
}
