using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Taskpilot.API.DTOs.Notifications;
using Taskpilot.API.Services;

namespace Taskpilot.API.Controllers;

/// <summary>
/// Endpoints for the current user's in-app notifications (the bell menu).
/// </summary>
[ApiController]
[Authorize]
[Route("api/notifications")]
public class NotificationsController : BaseApiController
{
    private readonly INotificationService _notifications;

    public NotificationsController(INotificationService notifications)
    {
        _notifications = notifications;
    }

    /// <summary>Lists the current user's notifications (use ?unreadOnly=true for unread).</summary>
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] bool unreadOnly = false)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _notifications.GetForUserAsync(userId.Value, unreadOnly);
        return Ok(result.Value);
    }

    /// <summary>Returns the number of unread notifications (for the bell badge).</summary>
    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _notifications.GetUnreadCountAsync(userId.Value);
        return Ok(new { count = result.Value });
    }

    /// <summary>Marks one notification as read.</summary>
    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _notifications.MarkReadAsync(userId.Value, id);
        return result.Succeeded
            ? Ok(new { message = "Marked as read." })
            : NotFound(new { error = result.Error });
    }

    /// <summary>Marks all of the current user's notifications as read.</summary>
    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead()
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        await _notifications.MarkAllReadAsync(userId.Value);
        return Ok(new { message = "All marked as read." });
    }

    /// <summary>Returns the notification types the current user has disabled.</summary>
    [HttpGet("preferences")]
    public async Task<IActionResult> GetPreferences()
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _notifications.GetDisabledTypesAsync(userId.Value);
        return Ok(new { disabledTypes = result.Value });
    }

    /// <summary>Replaces the current user's notification opt-outs.</summary>
    [HttpPut("preferences")]
    public async Task<IActionResult> UpdatePreferences([FromBody] UpdatePreferencesDto dto)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _notifications.SetDisabledTypesAsync(userId.Value, dto.DisabledTypes ?? new List<string>());
        return Ok(new { disabledTypes = result.Value });
    }
}
