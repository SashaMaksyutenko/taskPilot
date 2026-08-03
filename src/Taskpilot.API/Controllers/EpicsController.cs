using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Taskpilot.API.DTOs.Projects;
using Taskpilot.API.Services;

namespace Taskpilot.API.Controllers;

/// <summary>Epics for a project, plus moving a task into an epic.</summary>
[ApiController]
[Authorize]
public class EpicsController : BaseApiController
{
    private readonly IEpicService _epics;

    public EpicsController(IEpicService epics)
    {
        _epics = epics;
    }

    /// <summary>Lists a project's epics with task tallies.</summary>
    [HttpGet("api/projects/{projectId:guid}/epics")]
    public async Task<IActionResult> GetForProject(Guid projectId)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _epics.GetEpicsAsync(userId.Value, projectId);
        return result.Succeeded ? Ok(result.Value) : NotFound(new { error = result.Error });
    }

    /// <summary>Creates an epic in the project.</summary>
    [HttpPost("api/projects/{projectId:guid}/epics")]
    public async Task<IActionResult> Create(Guid projectId, [FromBody] SaveEpicDto dto)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _epics.CreateEpicAsync(userId.Value, projectId, dto);
        return result.Succeeded ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }

    /// <summary>Renames/recolours an epic.</summary>
    [HttpPut("api/epics/{epicId:guid}")]
    public async Task<IActionResult> Update(Guid epicId, [FromBody] SaveEpicDto dto)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _epics.UpdateEpicAsync(userId.Value, epicId, dto);
        return result.Succeeded ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }

    /// <summary>Deletes an epic; its tasks become ungrouped.</summary>
    [HttpDelete("api/epics/{epicId:guid}")]
    public async Task<IActionResult> Delete(Guid epicId)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _epics.DeleteEpicAsync(userId.Value, epicId);
        return result.Succeeded ? NoContent() : BadRequest(new { error = result.Error });
    }

    /// <summary>Moves a task into an epic, or ungroups it when epicId is null.</summary>
    [HttpPost("api/tasks/{taskId:guid}/epic")]
    public async Task<IActionResult> AssignTask(Guid taskId, [FromBody] AssignEpicDto dto)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _epics.AssignTaskAsync(userId.Value, taskId, dto.EpicId);
        return result.Succeeded ? NoContent() : BadRequest(new { error = result.Error });
    }
}
