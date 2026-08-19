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
    private readonly IBillingService _billing;

    public TaskAiController(ITaskAiService taskAi, IBillingService billing)
    {
        _taskAi = taskAi;
        _billing = billing;
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

    /// <summary>Extracts action-item task titles from pasted meeting notes (Pro feature).</summary>
    [HttpPost("api/projects/{projectId:guid}/ai/extract-tasks")]
    public async Task<IActionResult> ExtractTasks(Guid projectId, [FromBody] ExtractTasksDto dto)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();
        if (!await _billing.IsProAsync())
            return StatusCode(StatusCodes.Status402PaymentRequired, new { error = "AI features are on the Pro plan. Upgrade to use them." });

        var result = await _taskAi.ExtractTasksFromNotesAsync(userId.Value, projectId, dto.Notes ?? string.Empty);
        return result.Succeeded
            ? Ok(new { tasks = result.Value })
            : BadRequest(new { error = result.Error });
    }
}

/// <summary>Free-form meeting notes to extract tasks from.</summary>
public class ExtractTasksDto
{
    public string? Notes { get; set; }
}
