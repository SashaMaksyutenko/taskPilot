using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Taskpilot.API.DTOs.Calendar;
using Taskpilot.API.Services;

namespace Taskpilot.API.Controllers;

/// <summary>
/// Google Calendar sync: connect (OAuth), disconnect, status, and an on-demand push of the
/// user's deadline tasks to their Google Calendar. Config-gated on Google OAuth.
/// </summary>
[ApiController]
[Authorize]
[Route("api/calendar/google")]
public class GoogleCalendarController : BaseApiController
{
    private readonly IGoogleCalendarSyncService _sync;

    public GoogleCalendarController(IGoogleCalendarSyncService sync)
    {
        _sync = sync;
    }

    /// <summary>Whether the feature is configured and whether the current user is connected.</summary>
    [HttpGet("status")]
    public async Task<IActionResult> Status()
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();
        return Ok(await _sync.GetStatusAsync(userId.Value));
    }

    /// <summary>
    /// Builds the Google consent URL the frontend redirects to. The same redirectUri must be
    /// used on the follow-up connect call (Google validates it against the code).
    /// </summary>
    [HttpGet("connect-url")]
    public IActionResult ConnectUrl([FromQuery] string redirectUri)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();
        if (string.IsNullOrWhiteSpace(redirectUri)) return BadRequest(new { error = "redirectUri is required." });

        // State ties the callback to this user (the frontend echoes it back).
        var url = _sync.BuildConnectUrl(redirectUri, userId.Value.ToString());
        return Ok(new { url });
    }

    /// <summary>Completes the consent flow with the code returned by Google.</summary>
    [HttpPost("connect")]
    public async Task<IActionResult> Connect([FromBody] GoogleCalendarConnectDto dto)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _sync.ConnectAsync(userId.Value, dto.Code, dto.RedirectUri);
        if (!result.Succeeded) return BadRequest(new { error = result.Error });
        return Ok(await _sync.GetStatusAsync(userId.Value));
    }

    /// <summary>Pushes the current user's deadline tasks to their Google Calendar.</summary>
    [HttpPost("sync")]
    public async Task<IActionResult> Sync()
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _sync.SyncAsync(userId.Value);
        if (!result.Succeeded) return BadRequest(new { error = result.Error });
        return Ok(result.Value);
    }

    /// <summary>Disconnects the user's Google Calendar and forgets the task↔event links.</summary>
    [HttpDelete]
    public async Task<IActionResult> Disconnect()
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        await _sync.DisconnectAsync(userId.Value);
        return NoContent();
    }
}
