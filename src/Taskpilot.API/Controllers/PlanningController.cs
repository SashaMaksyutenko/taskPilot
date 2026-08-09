using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Taskpilot.API.Services;

namespace Taskpilot.API.Controllers;

/// <summary>AI-assisted planning: "what should I do next?" across the user's open tasks.</summary>
[ApiController]
[Authorize]
[Route("api/planning")]
public class PlanningController : BaseApiController
{
    private readonly INextActionService _planner;

    public PlanningController(INextActionService planner)
    {
        _planner = planner;
    }

    /// <summary>A prioritized plan of the next tasks to work on (AI-ranked when configured).</summary>
    [HttpGet("next")]
    public async Task<IActionResult> Next([FromQuery] int limit = 8)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        return Ok(await _planner.GetPlanAsync(userId.Value, limit));
    }
}
