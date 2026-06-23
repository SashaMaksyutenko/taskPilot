using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Taskpilot.API.Services;

namespace Taskpilot.API.Controllers;

/// <summary>
/// Public site statistics (forum-style footer). Open to everyone, including
/// guests — exposes only safe aggregate numbers, not the admin analytics.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/stats")]
public class StatsController : ControllerBase
{
    private readonly IStatsService _stats;

    public StatsController(IStatsService stats)
    {
        _stats = stats;
    }

    /// <summary>Returns public stats: totals, newest user and who is online.</summary>
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var result = await _stats.GetPublicStatsAsync();
        return Ok(result.Value);
    }
}
