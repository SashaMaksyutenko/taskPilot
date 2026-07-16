using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Taskpilot.API.Services;

namespace Taskpilot.API.Controllers;

/// <summary>AI helpers for tasks (subtask suggestions). All endpoints require authentication.</summary>
[ApiController]
[Authorize]
public class TaskAiController : BaseApiController
{
    private readonly ITaskAiService _taskAi;

    public TaskAiController(ITaskAiService taskAi)
    {
        _taskAi = taskAi;
    }

    /// <summary>Whether AI features are configured and available.</summary>
    [HttpGet("api/tasks/ai/status")]
    public IActionResult Status() => Ok(new { enabled = _taskAi.IsEnabled });

    /// <summary>Suggests a checklist of subtasks for a task (the user then picks which to add).</summary>
    [HttpPost("api/tasks/{taskId:guid}/ai/subtasks")]
    public async Task<IActionResult> SuggestSubtasks(Guid taskId)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _taskAi.SuggestSubtasksAsync(userId.Value, taskId);
        return result.Succeeded
            ? Ok(new { suggestions = result.Value })
            : BadRequest(new { error = result.Error });
    }
}
