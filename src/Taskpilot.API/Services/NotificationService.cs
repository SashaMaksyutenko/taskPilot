using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Taskpilot.API.Common;
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
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        TaskpilotDbContext context,
        IHubContext<NotificationHub> hub,
        ILogger<NotificationService> logger)
    {
        _context = context;
        _hub = hub;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task CreateAsync(Guid recipientId, NotificationType type, string message, string? link = null)
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
}
