using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Taskpilot.API.DTOs.Projects;
using Taskpilot.API.Services;

namespace Taskpilot.API.Controllers;

/// <summary>
/// GitHub integration: connect a repo to a project, read a task's linked commits/PRs, and the
/// public webhook endpoint GitHub POSTs push/pull_request events to.
/// </summary>
[ApiController]
[Authorize]
[Route("api")]
public class GitHubController : BaseApiController
{
    private readonly IGitHubIntegrationService _github;

    public GitHubController(IGitHubIntegrationService github)
    {
        _github = github;
    }

    /// <summary>Links a repository ("owner/name") and returns the webhook URL + secret to configure in GitHub.</summary>
    [HttpPost("projects/{projectId:guid}/github/connect")]
    public async Task<IActionResult> Connect(Guid projectId, [FromBody] GitHubConnectDto dto)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _github.ConnectRepoAsync(userId.Value, projectId, dto.Repo);
        if (!result.Succeeded) return BadRequest(new { error = result.Error });

        result.Value!.WebhookUrl = WebhookUrl(projectId);
        return Ok(result.Value);
    }

    /// <summary>Whether the project is connected and to which repo (any member).</summary>
    [HttpGet("projects/{projectId:guid}/github")]
    public async Task<IActionResult> Status(Guid projectId)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _github.GetStatusAsync(userId.Value, projectId);
        if (!result.Succeeded) return BadRequest(new { error = result.Error });
        if (result.Value!.Connected) result.Value.WebhookUrl = WebhookUrl(projectId);
        return Ok(result.Value);
    }

    /// <summary>Unlinks the repository (owner only).</summary>
    [HttpDelete("projects/{projectId:guid}/github")]
    public async Task<IActionResult> Disconnect(Guid projectId)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _github.DisconnectRepoAsync(userId.Value, projectId);
        if (!result.Succeeded) return BadRequest(new { error = result.Error });
        return NoContent();
    }

    /// <summary>Commits/PRs that reference the given task.</summary>
    [HttpGet("tasks/{taskId:guid}/github")]
    public async Task<IActionResult> TaskLinks(Guid taskId)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _github.GetTaskLinksAsync(userId.Value, taskId);
        if (!result.Succeeded) return BadRequest(new { error = result.Error });
        return Ok(result.Value);
    }

    /// <summary>Public webhook GitHub POSTs to; verified by the per-project HMAC secret.</summary>
    [HttpPost("github/webhook/{projectId:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> Webhook(Guid projectId)
    {
        // Read the exact bytes GitHub signed (must not be model-bound first).
        string rawBody;
        using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
            rawBody = await reader.ReadToEndAsync();

        var eventType = Request.Headers["X-GitHub-Event"].ToString();
        var signature = Request.Headers["X-Hub-Signature-256"].ToString();

        var result = await _github.HandleWebhookAsync(projectId, eventType, rawBody, signature);
        if (!result.Succeeded)
        {
            // A bad signature is an auth failure; everything else is a bad request.
            if (result.Error is not null && result.Error.Contains("signature", StringComparison.OrdinalIgnoreCase))
                return Unauthorized(new { error = result.Error });
            return BadRequest(new { error = result.Error });
        }
        return Ok(result.Value);
    }

    private string WebhookUrl(Guid projectId) =>
        $"{Request.Scheme}://{Request.Host}/api/github/webhook/{projectId}";
}
