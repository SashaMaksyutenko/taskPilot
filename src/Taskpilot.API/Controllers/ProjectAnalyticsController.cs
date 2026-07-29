using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Taskpilot.API.Services;

namespace Taskpilot.API.Controllers;

/// <summary>Delivery analytics for a project board (any member may view).</summary>
[ApiController]
[Authorize]
[Route("api/projects")]
public class ProjectAnalyticsController : BaseApiController
{
    private readonly IProjectAnalyticsService _analytics;

    public ProjectAnalyticsController(IProjectAnalyticsService analytics)
    {
        _analytics = analytics;
    }

    /// <summary>Returns the project's aggregate metrics (status/priority mix, weekly trend, cycle time, workload).</summary>
    [HttpGet("{projectId:guid}/analytics")]
    public async Task<IActionResult> Get(Guid projectId)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _analytics.GetAnalyticsAsync(userId.Value, projectId);
        return result.Succeeded ? Ok(result.Value) : NotFound(new { error = result.Error });
    }
}
