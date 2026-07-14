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
    private readonly ITelegramLinkService _telegramLink;
    private readonly IViberLinkService _viberLink;
    private readonly IPushService _push;

    public NotificationsController(INotificationService notifications, ITelegramLinkService telegramLink, IViberLinkService viberLink, IPushService push)
    {
        _notifications = notifications;
        _telegramLink = telegramLink;
        _viberLink = viberLink;
        _push = push;
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

        var inApp = await _notifications.GetDisabledTypesAsync(userId.Value);
        var email = await _notifications.GetDisabledEmailTypesAsync(userId.Value);
        var digest = await _notifications.GetDigestFrequencyAsync(userId.Value);
        var quiet = await _notifications.GetQuietHoursAsync(userId.Value);
        return Ok(new
        {
            disabledTypes = inApp.Value,
            disabledEmailTypes = email.Value,
            digestFrequency = digest.Value,
            quietHours = quiet.Value,
        });
    }

    /// <summary>
    /// Sets the current user's quiet-hours window. Inside it, email/Telegram/Viber/push
    /// are held back; in-app notifications still arrive in the bell.
    /// </summary>
    [HttpPut("quiet-hours")]
    public async Task<IActionResult> UpdateQuietHours([FromBody] QuietHoursDto dto)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _notifications.SetQuietHoursAsync(userId.Value, dto);
        return result.Succeeded
            ? Ok(result.Value)
            : BadRequest(new { error = result.Error });
    }

    /// <summary>Sets how often the current user receives a digest email (Off/Daily/Weekly).</summary>
    [HttpPut("digest")]
    public async Task<IActionResult> UpdateDigest([FromBody] UpdateDigestDto dto)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _notifications.SetDigestFrequencyAsync(userId.Value, dto.Frequency);
        return result.Succeeded
            ? Ok(new { digestFrequency = result.Value })
            : BadRequest(new { error = result.Error });
    }

    /// <summary>Replaces the current user's notification opt-outs (in-app and email).</summary>
    [HttpPut("preferences")]
    public async Task<IActionResult> UpdatePreferences([FromBody] UpdatePreferencesDto dto)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var inApp = await _notifications.SetDisabledTypesAsync(userId.Value, dto.DisabledTypes ?? new List<string>());
        var email = await _notifications.SetDisabledEmailTypesAsync(userId.Value, dto.DisabledEmailTypes ?? new List<string>());
        return Ok(new { disabledTypes = inApp.Value, disabledEmailTypes = email.Value });
    }

    /// <summary>Whether the current user has linked Telegram (and the bot username).</summary>
    [HttpGet("telegram")]
    public async Task<IActionResult> TelegramStatus()
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _telegramLink.GetStatusAsync(userId.Value);
        return Ok(new { linked = result.Value!.Linked, botUsername = result.Value.BotUsername });
    }

    /// <summary>Generates a one-time code to link Telegram; the user sends it to the bot.</summary>
    [HttpPost("telegram/link-code")]
    public async Task<IActionResult> TelegramLinkCode()
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _telegramLink.CreateLinkCodeAsync(userId.Value);
        return result.Succeeded
            ? Ok(new { code = result.Value.Code, botUsername = result.Value.BotUsername })
            : BadRequest(new { error = result.Error });
    }

    /// <summary>Unlinks the current user's Telegram.</summary>
    [HttpDelete("telegram")]
    public async Task<IActionResult> TelegramUnlink()
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        await _telegramLink.UnlinkAsync(userId.Value);
        return Ok(new { message = "Telegram unlinked." });
    }

    /// <summary>Whether the current user has linked Viber (and the bot name).</summary>
    [HttpGet("viber")]
    public async Task<IActionResult> ViberStatus()
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _viberLink.GetStatusAsync(userId.Value);
        return Ok(new { linked = result.Value!.Linked, botName = result.Value.BotName });
    }

    /// <summary>Generates a one-time code to link Viber; the user sends it to the bot.</summary>
    [HttpPost("viber/link-code")]
    public async Task<IActionResult> ViberLinkCode()
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _viberLink.CreateLinkCodeAsync(userId.Value);
        return result.Succeeded
            ? Ok(new { code = result.Value.Code, botName = result.Value.BotName })
            : BadRequest(new { error = result.Error });
    }

    /// <summary>Unlinks the current user's Viber.</summary>
    [HttpDelete("viber")]
    public async Task<IActionResult> ViberUnlink()
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        await _viberLink.UnlinkAsync(userId.Value);
        return Ok(new { message = "Viber unlinked." });
    }

    /// <summary>Returns the public VAPID key the browser needs to subscribe to push (empty when disabled).</summary>
    [HttpGet("push/vapid-key")]
    [AllowAnonymous]
    public IActionResult VapidKey() => Ok(new { publicKey = _push.PublicKey });

    /// <summary>Registers the current browser for Web Push.</summary>
    [HttpPost("push/subscribe")]
    public async Task<IActionResult> PushSubscribe([FromBody] PushSubscriptionDto dto)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _push.SubscribeAsync(userId.Value, dto.Endpoint, dto.P256dh, dto.Auth);
        return result.Succeeded ? Ok(new { message = "Subscribed." }) : BadRequest(new { error = result.Error });
    }

    /// <summary>Removes the current browser's Web Push subscription.</summary>
    [HttpPost("push/unsubscribe")]
    public async Task<IActionResult> PushUnsubscribe([FromBody] PushUnsubscribeDto dto)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        await _push.UnsubscribeAsync(userId.Value, dto.Endpoint);
        return Ok(new { message = "Unsubscribed." });
    }
}
