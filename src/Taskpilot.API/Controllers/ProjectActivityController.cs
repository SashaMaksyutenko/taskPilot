using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Taskpilot.API.Services;

namespace Taskpilot.API.Controllers;

/// <summary>A project's activity feed (recent task actions from the audit trail).</summary>
[ApiController]
[Authorize]
public class ProjectActivityController : BaseApiController
{
    private readonly IActivityService _activity;

    public ProjectActivityController(IActivityService activity)
    {
        _activity = activity;
    }

    /// <summary>Recent task actions in a project, newest first.</summary>
    [HttpGet("api/projects/{projectId:guid}/activity")]
    public async Task<IActionResult> Get(Guid projectId, [FromQuery] int limit = 30)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _activity.GetProjectActivityAsync(userId.Value, projectId, limit);
        return result.Succeeded ? Ok(result.Value) : NotFound(new { error = result.Error });
    }
}
