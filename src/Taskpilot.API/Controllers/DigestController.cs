using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Taskpilot.API.Services;

namespace Taskpilot.API.Controllers;

/// <summary>The current user's weekly activity digest (numbers + optional AI narrative).</summary>
[ApiController]
[Authorize]
[Route("api/digest")]
public class DigestController : BaseApiController
{
    private readonly IWeeklyDigestService _digest;

    public DigestController(IWeeklyDigestService digest)
    {
        _digest = digest;
    }

    /// <summary>Week-in-review numbers (no LLM call).</summary>
    [HttpGet("weekly")]
    public async Task<IActionResult> Weekly()
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        return Ok(await _digest.GetWeeklyAsync(userId.Value));
    }

    /// <summary>An AI-written summary of the week (call on demand — it uses the LLM).</summary>
    [HttpGet("weekly/summary")]
    public async Task<IActionResult> Summary()
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _digest.GetSummaryAsync(userId.Value);
        return result.Succeeded ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }
}
