using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Taskpilot.API.Common;
using Taskpilot.API.Configuration;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Notifications;
using Taskpilot.API.Hubs;
using Taskpilot.API.Messages;
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
    private readonly INotificationDeliveryService _delivery;
    private readonly INotificationQueue _queue;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        TaskpilotDbContext context,
        IHubContext<NotificationHub> hub,
        INotificationDeliveryService delivery,
        INotificationQueue queue,
        ILogger<NotificationService> logger)
    {
        _context = context;
        _hub = hub;
        _delivery = delivery;
        _queue = queue;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task CreateAsync(Guid recipientId, NotificationType type, string message, string? link = null)
    {
        // In-app is opted out independently per (type, InApp channel).
        // (Email muting is applied by the delivery service, inline or queued.)
        var inAppMuted = await _context.NotificationPreferences
            .AnyAsync(p => p.UserId == recipientId && p.Type == type && p.Channel == NotificationChannel.InApp);

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

        // Out-of-band channels (email, Telegram, Viber, push): offload to the queue
        // when RabbitMQ is enabled, otherwise deliver inline (unchanged behaviour).
        if (_queue.IsEnabled)
            await _queue.PublishAsync(new NotificationDeliveryMessage(recipientId, type, message, link));
        else
            await _delivery.DeliverAsync(recipientId, type, message, link);
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

    /// <inheritdoc />
    public async Task<Result<string>> GetDigestFrequencyAsync(Guid userId)
    {
        var freq = await _context.Users
            .Where(u => u.Id == userId)
            .Select(u => u.DigestFrequency)
            .FirstOrDefaultAsync();
        return Result<string>.Ok(freq.ToString());
    }

    /// <inheritdoc />
    public async Task<Result<string>> SetDigestFrequencyAsync(Guid userId, string frequency)
    {
        if (!Enum.TryParse<DigestFrequency>(frequency, ignoreCase: true, out var freq))
            return Result<string>.Fail("Invalid digest frequency.");

        var updated = await _context.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.DigestFrequency, freq));
        if (updated == 0)
            return Result<string>.Fail("User not found.");

        _logger.LogInformation("Digest frequency updated. UserId: {UserId}, Frequency: {Frequency}", userId, freq);
        return Result<string>.Ok(freq.ToString());
    }

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
