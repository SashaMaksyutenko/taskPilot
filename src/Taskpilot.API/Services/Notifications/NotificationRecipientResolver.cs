using Microsoft.EntityFrameworkCore;
using Taskpilot.API.Common;
using Taskpilot.API.Data;
using Taskpilot.API.Models;

namespace Taskpilot.API.Services;

/// <summary>
/// Loads a recipient's contact details and email-mute flag into a <see cref="NotificationRecipient"/>
/// snapshot the dispatcher can act on without any data access — or returns null when the
/// recipient is inside their quiet hours, so the caller suppresses all out-of-band delivery.
/// </summary>
public class NotificationRecipientResolver : INotificationRecipientResolver
{
    private readonly TaskpilotDbContext _context;

    public NotificationRecipientResolver(TaskpilotDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<NotificationRecipient?> ResolveAsync(Guid recipientId, NotificationType type)
    {
        // Contact details and quiet-hours settings come from the one user row.
        var user = await _context.Users
            .Where(u => u.Id == recipientId)
            .Select(u => new
            {
                u.Email, u.Name, u.TelegramChatId, u.ViberId,
                u.QuietHoursEnabled, u.QuietHoursStart, u.QuietHoursEnd, u.TimeZoneId,
            })
            .AsNoTracking()
            .FirstOrDefaultAsync();

        // Quiet hours hold back every out-of-band channel. The in-app notification was already
        // stored by the caller, so nothing is lost — the bell just waits.
        if (user is not null && user.QuietHoursEnabled &&
            QuietHours.IsQuiet(user.QuietHoursStart, user.QuietHoursEnd, user.TimeZoneId, DateTime.UtcNow))
            return null;

        // Email is opted out independently per (type, Email channel).
        var emailMuted = await _context.NotificationPreferences
            .AnyAsync(p => p.UserId == recipientId && p.Type == type && p.Channel == NotificationChannel.Email);

        return new NotificationRecipient(
            user?.Email, user?.Name, user?.TelegramChatId, user?.ViberId, emailMuted);
    }
}
