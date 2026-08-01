using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Taskpilot.API.DTOs.Projects;
using Taskpilot.API.Services;

namespace Taskpilot.API.Controllers;

/// <summary>Per-column work-in-progress (WIP) limits for a project's Kanban board.</summary>
[ApiController]
[Authorize]
public class WipLimitsController : BaseApiController
{
    private readonly IWipLimitService _wipLimits;

    public WipLimitsController(IWipLimitService wipLimits)
    {
        _wipLimits = wipLimits;
    }

    /// <summary>Lists a project's WIP limits.</summary>
    [HttpGet("api/projects/{projectId:guid}/wip-limits")]
    public async Task<IActionResult> Get(Guid projectId)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _wipLimits.GetAsync(userId.Value, projectId);
        return result.Succeeded ? Ok(result.Value) : NotFound(new { error = result.Error });
    }

    /// <summary>Sets or clears a column's WIP limit.</summary>
    [HttpPut("api/projects/{projectId:guid}/wip-limits")]
    public async Task<IActionResult> Set(Guid projectId, [FromBody] SetWipLimitDto dto)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _wipLimits.SetAsync(userId.Value, projectId, dto);
        return result.Succeeded ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }
}
