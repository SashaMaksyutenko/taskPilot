using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Taskpilot.API.DTOs.Projects;
using Taskpilot.API.Services;

namespace Taskpilot.API.Controllers;

/// <summary>Endpoints for task deadline-extension requests. All require authentication.</summary>
[ApiController]
[Authorize]
public class TaskExtensionsController : BaseApiController
{
    private readonly IExtensionService _extensions;

    public TaskExtensionsController(IExtensionService extensions)
    {
        _extensions = extensions;
    }

    /// <summary>Lists a task's extension requests (newest first).</summary>
    [HttpGet("api/tasks/{taskId:guid}/extension-requests")]
    public async Task<IActionResult> GetForTask(Guid taskId)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _extensions.GetForTaskAsync(userId.Value, taskId);
        return result.Succeeded ? Ok(result.Value) : NotFound(new { error = result.Error });
    }

    /// <summary>Raises a pending extension request for a task.</summary>
    [HttpPost("api/tasks/{taskId:guid}/extension-requests")]
    public async Task<IActionResult> RequestExtension(Guid taskId, [FromBody] CreateExtensionRequestDto dto)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _extensions.RequestAsync(userId.Value, taskId, dto);
        return result.Succeeded
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : BadRequest(new { error = result.Error });
    }

    /// <summary>Approves or rejects an extension request (project owner only).</summary>
    [HttpPost("api/extension-requests/{requestId:guid}/decision")]
    public async Task<IActionResult> Decide(Guid requestId, [FromBody] DecideExtensionDto dto)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _extensions.DecideAsync(userId.Value, requestId, dto.Approve);
        return result.Succeeded ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }
}
