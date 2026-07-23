using Microsoft.EntityFrameworkCore;
using Taskpilot.API.Data;
using Taskpilot.API.Models;

namespace Taskpilot.API.Services;

/// <summary>
/// Loads a recipient's contact details and email-mute flag from the database into a
/// <see cref="NotificationRecipient"/> snapshot the dispatcher can act on without any data access.
/// </summary>
public class NotificationRecipientResolver : INotificationRecipientResolver
{
    private readonly TaskpilotDbContext _context;

    public NotificationRecipientResolver(TaskpilotDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<NotificationRecipient> ResolveAsync(Guid recipientId, NotificationType type)
    {
        // Email is opted out independently per (type, Email channel).
        var emailMuted = await _context.NotificationPreferences
            .AnyAsync(p => p.UserId == recipientId && p.Type == type && p.Channel == NotificationChannel.Email);

        var user = await _context.Users
            .Where(u => u.Id == recipientId)
            .Select(u => new { u.Email, u.Name, u.TelegramChatId, u.ViberId })
            .AsNoTracking()
            .FirstOrDefaultAsync();

        return new NotificationRecipient(
            user?.Email, user?.Name, user?.TelegramChatId, user?.ViberId, emailMuted);
    }
}
