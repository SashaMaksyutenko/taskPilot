using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Taskpilot.API.Services;

namespace Taskpilot.API.Controllers;

/// <summary>Task watchers — subscribe to a task's notifications without being its assignee.</summary>
[ApiController]
[Authorize]
public class TaskWatchersController : BaseApiController
{
    private readonly ITaskWatcherService _watchers;

    public TaskWatchersController(ITaskWatcherService watchers)
    {
        _watchers = watchers;
    }

    /// <summary>Returns a task's watchers and whether the caller is watching it.</summary>
    [HttpGet("api/tasks/{taskId:guid}/watchers")]
    public async Task<IActionResult> Get(Guid taskId)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _watchers.GetAsync(userId.Value, taskId);
        return result.Succeeded ? Ok(result.Value) : NotFound(new { error = result.Error });
    }

    /// <summary>Subscribes the caller to a task's notifications.</summary>
    [HttpPost("api/tasks/{taskId:guid}/watch")]
    public async Task<IActionResult> Watch(Guid taskId)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _watchers.WatchAsync(userId.Value, taskId);
        return result.Succeeded ? Ok(result.Value) : NotFound(new { error = result.Error });
    }

    /// <summary>Unsubscribes the caller from a task's notifications.</summary>
    [HttpDelete("api/tasks/{taskId:guid}/watch")]
    public async Task<IActionResult> Unwatch(Guid taskId)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _watchers.UnwatchAsync(userId.Value, taskId);
        return result.Succeeded ? Ok(result.Value) : NotFound(new { error = result.Error });
    }
}
