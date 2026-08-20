using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Taskpilot.API.DTOs.Integrations;
using Taskpilot.API.Services;

namespace Taskpilot.API.Controllers;

/// <summary>
/// Per-user GitHub account link (outbound OAuth): connect a personal GitHub account, see status,
/// disconnect, and list the linked account's repositories. Separate from the project-repo webhook
/// integration in <see cref="GitHubController"/>. Config-gated on the GitHubIntegration OAuth app.
/// </summary>
[ApiController]
[Authorize]
[Route("api/integrations/github")]
public class GitHubConnectionController : BaseApiController
{
    private readonly IGitHubConnectionService _github;

    public GitHubConnectionController(IGitHubConnectionService github)
    {
        _github = github;
    }

    /// <summary>Whether the feature is configured and whether the current user is linked.</summary>
    [HttpGet("status")]
    public async Task<IActionResult> Status()
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();
        return Ok(await _github.GetStatusAsync(userId.Value));
    }

    /// <summary>
    /// Builds the GitHub authorize URL the frontend redirects to. The same redirectUri must be used
    /// on the follow-up connect call (GitHub validates it against the code).
    /// </summary>
    [HttpGet("connect-url")]
    public IActionResult ConnectUrl([FromQuery] string redirectUri)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = _github.BuildConnectUrl(userId.Value, redirectUri);
        if (!result.Succeeded) return BadRequest(new { error = result.Error });
        return Ok(new { url = result.Value });
    }

    /// <summary>Completes the link with the code GitHub returned.</summary>
    [HttpPost("connect")]
    public async Task<IActionResult> Connect([FromBody] GitHubConnectDto dto)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _github.ConnectAsync(userId.Value, dto.Code, dto.RedirectUri, dto.State);
        if (!result.Succeeded) return BadRequest(new { error = result.Error });
        return Ok(await _github.GetStatusAsync(userId.Value));
    }

    /// <summary>Lists the repositories the linked GitHub account can access.</summary>
    [HttpGet("repos")]
    public async Task<IActionResult> Repos()
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _github.GetReposAsync(userId.Value);
        if (!result.Succeeded) return BadRequest(new { error = result.Error });
        return Ok(result.Value);
    }

    /// <summary>Unlinks the current user's GitHub account.</summary>
    [HttpDelete]
    public async Task<IActionResult> Disconnect()
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        await _github.DisconnectAsync(userId.Value);
        return NoContent();
    }
}
