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
    private readonly EmailOptions _emailOptions;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        TaskpilotDbContext context,
        IHubContext<NotificationHub> hub,
        IEmailSender email,
        IOptions<EmailOptions> emailOptions,
        ILogger<NotificationService> logger)
    {
        _context = context;
        _hub = hub;
        _email = email;
        _emailOptions = emailOptions.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task CreateAsync(Guid recipientId, NotificationType type, string message, string? link = null)
    {
        // Respect the recipient's preferences: skip types they have opted out of.
        if (await _context.NotificationPreferences.AnyAsync(p => p.UserId == recipientId && p.Type == type))
        {
            _logger.LogInformation("Notification suppressed by preference. RecipientId: {RecipientId}, Type: {Type}", recipientId, type);
            return;
        }

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

        // Push to the recipient's connected clients (if any) so the bell updates live.
        await _hub.Clients.Group(NotificationHub.GroupName(recipientId)).SendAsync("ReceiveNotification", new NotificationDto
        {
            Id = notification.Id,
            Type = notification.Type.ToString(),
            Message = notification.Message,
            Link = notification.Link,
            IsRead = notification.IsRead,
            CreatedAt = notification.CreatedAt,
        });

        // Also deliver by email when a provider is configured (best-effort).
        await SendEmailAsync(recipientId, message, link);
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
    public async Task<Result<List<string>>> GetDisabledTypesAsync(Guid userId)
    {
        var types = await _context.NotificationPreferences
            .Where(p => p.UserId == userId)
            .Select(p => p.Type)
            .ToListAsync();

        return Result<List<string>>.Ok(types.Select(t => t.ToString()).ToList());
    }

    /// <inheritdoc />
    public async Task<Result<List<string>>> SetDisabledTypesAsync(Guid userId, IEnumerable<string> typeNames)
    {
        // Keep only valid, distinct type names.
        var disabled = typeNames
            .Select(n => Enum.TryParse<NotificationType>(n, ignoreCase: true, out var t) ? t : (NotificationType?)null)
            .Where(t => t.HasValue)
            .Select(t => t!.Value)
            .Distinct()
            .ToList();

        // Replace the user's opt-out set entirely.
        var existing = await _context.NotificationPreferences
            .Where(p => p.UserId == userId)
            .ToListAsync();
        _context.NotificationPreferences.RemoveRange(existing);

        foreach (var type in disabled)
            _context.NotificationPreferences.Add(new NotificationPreference
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Type = type,
            });

        await _context.SaveChangesAsync();
        _logger.LogInformation("Notification preferences updated. UserId: {UserId}, Disabled: {Count}", userId, disabled.Count);

        return Result<List<string>>.Ok(disabled.Select(t => t.ToString()).ToList());
    }
}
