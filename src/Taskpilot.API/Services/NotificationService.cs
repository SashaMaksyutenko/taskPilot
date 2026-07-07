using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Taskpilot.API.Common;
using Taskpilot.API.Configuration;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Notifications;
using Taskpilot.API.Hubs;
using Taskpilot.API.Models;

namespace Taskpilot.API.Services;

/// <summary>
/// Stores and reads in-app notifications in the database, and pushes new ones to
/// the recipient in real time over the notification hub.
/// </summary>
public class NotificationService : INotificationService
{
    private readonly TaskpilotDbContext _context;
    private readonly IHubContext<NotificationHub> _hub;
    private readonly IEmailSender _email;
    private readonly ITelegramSender _telegram;
    private readonly IPushService _push;
    private readonly EmailOptions _emailOptions;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        TaskpilotDbContext context,
        IHubContext<NotificationHub> hub,
        IEmailSender email,
        ITelegramSender telegram,
        IPushService push,
        IOptions<EmailOptions> emailOptions,
        ILogger<NotificationService> logger)
    {
        _context = context;
        _hub = hub;
        _email = email;
        _telegram = telegram;
        _push = push;
        _emailOptions = emailOptions.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task CreateAsync(Guid recipientId, NotificationType type, string message, string? link = null)
    {
        // The two channels are opted out independently.
        var muted = await _context.NotificationPreferences
            .Where(p => p.UserId == recipientId && p.Type == type)
            .Select(p => p.Channel)
            .ToListAsync();
        var inAppMuted = muted.Contains(NotificationChannel.InApp);
        var emailMuted = muted.Contains(NotificationChannel.Email);

        // In-app: store and push in real time unless muted.
        if (!inAppMuted)
        {
            var notification = new Notification
            {
                Id = Guid.NewGuid(),
                RecipientId = recipientId,
                Type = type,
                Message = message,
                Link = link,
                IsRead = false,
                CreatedAt = DateTime.UtcNow,
            };
            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Notification created. RecipientId: {RecipientId}, Type: {Type}", recipientId, type);

            await _hub.Clients.Group(NotificationHub.GroupName(recipientId)).SendAsync("ReceiveNotification", new NotificationDto
            {
                Id = notification.Id,
                Type = notification.Type.ToString(),
                Message = notification.Message,
                Link = notification.Link,
                IsRead = notification.IsRead,
                CreatedAt = notification.CreatedAt,
            });
        }

        // Email: deliver when configured and not muted for this type (best-effort).
        if (!emailMuted)
            await SendEmailAsync(recipientId, message, link);

        // Telegram: deliver to linked users (best-effort).
        await SendTelegramAsync(recipientId, message, link);

        // Web push: deliver to the user's subscribed browsers (best-effort).
        var pushUrl = string.IsNullOrEmpty(link)
            ? _emailOptions.FrontendBaseUrl
            : _emailOptions.FrontendBaseUrl.TrimEnd('/') + "/" + link.TrimStart('/');
        await _push.SendToUserAsync(recipientId, "TaskPilot", message, pushUrl);
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

        var url = string.IsNullOrEmpty(link)
            ? _emailOptions.FrontendBaseUrl
            : _emailOptions.FrontendBaseUrl.TrimEnd('/') + "/" + link.TrimStart('/');

        await _telegram.SendMessageAsync(chatId, $"{message}\n{url}");
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

        // Turn a relative link (e.g. "/projects/{id}") into a clickable absolute URL.
        var url = string.IsNullOrEmpty(link)
            ? _emailOptions.FrontendBaseUrl
            : _emailOptions.FrontendBaseUrl.TrimEnd('/') + "/" + link.TrimStart('/');

        var html =
            $"<p>Hi {System.Net.WebUtility.HtmlEncode(recipient.Name)},</p>" +
            $"<p>{System.Net.WebUtility.HtmlEncode(message)}</p>" +
            $"<p><a href=\"{url}\">Open in TaskPilot</a></p>";

        await _email.SendAsync(recipient.Email, recipient.Name, "TaskPilot notification", html);
    }

    /// <inheritdoc />
    public async Task<Result<List<NotificationDto>>> GetForUserAsync(Guid userId, bool unreadOnly)
    {
        var query = _context.Notifications.Where(n => n.RecipientId == userId);
        if (unreadOnly)
            query = query.Where(n => !n.IsRead);

        var items = await query
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new NotificationDto
            {
                Id = n.Id,
                Type = n.Type.ToString(),
                Message = n.Message,
                Link = n.Link,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt,
            })
            .AsNoTracking()
            .ToListAsync();

        return Result<List<NotificationDto>>.Ok(items);
    }

    /// <inheritdoc />
    public async Task<Result<int>> GetUnreadCountAsync(Guid userId)
    {
        var count = await _context.Notifications.CountAsync(n => n.RecipientId == userId && !n.IsRead);
        return Result<int>.Ok(count);
    }

    /// <inheritdoc />
    public async Task<Result> MarkReadAsync(Guid userId, Guid notificationId)
    {
        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.RecipientId == userId);
        if (notification is null)
            return Result.Fail("Notification not found.");

        notification.IsRead = true;
        await _context.SaveChangesAsync();
        return Result.Ok();
    }

    /// <inheritdoc />
    public async Task<Result> MarkAllReadAsync(Guid userId)
    {
        // Bulk update: set IsRead = true for all the user's unread notifications.
        await _context.Notifications
            .Where(n => n.RecipientId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
        return Result.Ok();
    }

    /// <inheritdoc />
    public Task<Result<List<string>>> GetDisabledTypesAsync(Guid userId) =>
        GetDisabledAsync(userId, NotificationChannel.InApp);

    /// <inheritdoc />
    public Task<Result<List<string>>> SetDisabledTypesAsync(Guid userId, IEnumerable<string> typeNames) =>
        SetDisabledAsync(userId, NotificationChannel.InApp, typeNames);

    /// <inheritdoc />
    public Task<Result<List<string>>> GetDisabledEmailTypesAsync(Guid userId) =>
        GetDisabledAsync(userId, NotificationChannel.Email);

    /// <inheritdoc />
    public Task<Result<List<string>>> SetDisabledEmailTypesAsync(Guid userId, IEnumerable<string> typeNames) =>
        SetDisabledAsync(userId, NotificationChannel.Email, typeNames);

    /// <summary>Returns the notification types the user muted on the given channel.</summary>
    private async Task<Result<List<string>>> GetDisabledAsync(Guid userId, NotificationChannel channel)
    {
        var types = await _context.NotificationPreferences
            .Where(p => p.UserId == userId && p.Channel == channel)
            .Select(p => p.Type)
            .ToListAsync();

        return Result<List<string>>.Ok(types.Select(t => t.ToString()).ToList());
    }

    /// <summary>Replaces the user's opt-out set for one channel (other channels untouched).</summary>
    private async Task<Result<List<string>>> SetDisabledAsync(Guid userId, NotificationChannel channel, IEnumerable<string> typeNames)
    {
        // Keep only valid, distinct type names.
        var disabled = typeNames
            .Select(n => Enum.TryParse<NotificationType>(n, ignoreCase: true, out var t) ? t : (NotificationType?)null)
            .Where(t => t.HasValue)
            .Select(t => t!.Value)
            .Distinct()
            .ToList();

        // Replace only this channel's opt-outs.
        var existing = await _context.NotificationPreferences
            .Where(p => p.UserId == userId && p.Channel == channel)
            .ToListAsync();
        _context.NotificationPreferences.RemoveRange(existing);

        foreach (var type in disabled)
            _context.NotificationPreferences.Add(new NotificationPreference
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Type = type,
                Channel = channel,
            });

        await _context.SaveChangesAsync();
        _logger.LogInformation("Notification preferences updated. UserId: {UserId}, Channel: {Channel}, Disabled: {Count}", userId, channel, disabled.Count);

        return Result<List<string>>.Ok(disabled.Select(t => t.ToString()).ToList());
    }
}
