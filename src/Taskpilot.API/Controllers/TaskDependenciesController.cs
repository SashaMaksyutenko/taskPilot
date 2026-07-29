using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Taskpilot.API.DTOs.Projects;
using Taskpilot.API.Services;

namespace Taskpilot.API.Controllers;

/// <summary>Task dependencies (blocked-by / blocks) for a single task.</summary>
[ApiController]
[Authorize]
public class TaskDependenciesController : BaseApiController
{
    private readonly ITaskDependencyService _dependencies;

    public TaskDependenciesController(ITaskDependencyService dependencies)
    {
        _dependencies = dependencies;
    }

    /// <summary>Returns a task's dependency graph.</summary>
    [HttpGet("api/tasks/{taskId:guid}/dependencies")]
    public async Task<IActionResult> Get(Guid taskId)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _dependencies.GetAsync(userId.Value, taskId);
        return result.Succeeded ? Ok(result.Value) : NotFound(new { error = result.Error });
    }

    /// <summary>Adds a dependency: this task depends on (is blocked by) another.</summary>
    [HttpPost("api/tasks/{taskId:guid}/dependencies")]
    public async Task<IActionResult> Add(Guid taskId, [FromBody] AddDependencyDto dto)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _dependencies.AddAsync(userId.Value, taskId, dto.DependsOnTaskId);
        return result.Succeeded ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }

    /// <summary>Removes a dependency edge.</summary>
    [HttpDelete("api/tasks/{taskId:guid}/dependencies/{dependsOnTaskId:guid}")]
    public async Task<IActionResult> Remove(Guid taskId, Guid dependsOnTaskId)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _dependencies.RemoveAsync(userId.Value, taskId, dependsOnTaskId);
        return result.Succeeded ? NoContent() : BadRequest(new { error = result.Error });
    }
}
