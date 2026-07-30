using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Taskpilot.API.DTOs.Projects;
using Taskpilot.API.Services;

namespace Taskpilot.API.Controllers;

/// <summary>Sprints / iterations for a project, plus moving a task into a sprint.</summary>
[ApiController]
[Authorize]
public class SprintsController : BaseApiController
{
    private readonly ISprintService _sprints;

    public SprintsController(ISprintService sprints)
    {
        _sprints = sprints;
    }

    /// <summary>Lists a project's sprints with task tallies.</summary>
    [HttpGet("api/projects/{projectId:guid}/sprints")]
    public async Task<IActionResult> GetForProject(Guid projectId)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _sprints.GetSprintsAsync(userId.Value, projectId);
        return result.Succeeded ? Ok(result.Value) : NotFound(new { error = result.Error });
    }

    /// <summary>Creates a sprint in the project.</summary>
    [HttpPost("api/projects/{projectId:guid}/sprints")]
    public async Task<IActionResult> Create(Guid projectId, [FromBody] SaveSprintDto dto)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _sprints.CreateSprintAsync(userId.Value, projectId, dto);
        return result.Succeeded ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }

    /// <summary>Updates a sprint (name, goal, dates, status).</summary>
    [HttpPut("api/sprints/{sprintId:guid}")]
    public async Task<IActionResult> Update(Guid sprintId, [FromBody] SaveSprintDto dto)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _sprints.UpdateSprintAsync(userId.Value, sprintId, dto);
        return result.Succeeded ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }

    /// <summary>Deletes a sprint; its tasks return to the backlog.</summary>
    [HttpDelete("api/sprints/{sprintId:guid}")]
    public async Task<IActionResult> Delete(Guid sprintId)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _sprints.DeleteSprintAsync(userId.Value, sprintId);
        return result.Succeeded ? NoContent() : BadRequest(new { error = result.Error });
    }

    /// <summary>Moves a task into a sprint, or out to the backlog when sprintId is null.</summary>
    [HttpPost("api/tasks/{taskId:guid}/sprint")]
    public async Task<IActionResult> AssignTask(Guid taskId, [FromBody] AssignSprintDto dto)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _sprints.AssignTaskAsync(userId.Value, taskId, dto.SprintId);
        return result.Succeeded ? NoContent() : BadRequest(new { error = result.Error });
    }
}
