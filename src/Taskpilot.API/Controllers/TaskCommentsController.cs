using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Taskpilot.API.DTOs.Projects;
using Taskpilot.API.Hubs;
using Taskpilot.API.Services;

namespace Taskpilot.API.Controllers;

/// <summary>
/// REST endpoints for task comments. List/add are nested under a task; delete uses
/// the comment id. All require authentication and are scoped to the owner of the
/// task's parent project.
/// </summary>
[ApiController]
[Authorize]
public class TaskCommentsController : BaseApiController
{
    private readonly ITaskCommentService _comments;
    private readonly IValidator<CreateCommentDto> _createValidator;
    private readonly IHubContext<TaskHub> _taskHub;

    public TaskCommentsController(
        ITaskCommentService comments,
        IValidator<CreateCommentDto> createValidator,
        IHubContext<TaskHub> taskHub)
    {
        _comments = comments;
        _createValidator = createValidator;
        _taskHub = taskHub;
    }

    /// <summary>Lists a task's comments, oldest first.</summary>
    [HttpGet("api/tasks/{taskId:guid}/comments")]
    public async Task<IActionResult> GetForTask(Guid taskId)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _comments.GetForTaskAsync(userId.Value, taskId);
        return result.Succeeded
            ? Ok(result.Value)
            : BadRequest(new { error = result.Error });
    }

    /// <summary>Adds a comment to a task.</summary>
    [HttpPost("api/tasks/{taskId:guid}/comments")]
    public async Task<IActionResult> Add(Guid taskId, [FromBody] CreateCommentDto dto)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var validation = await _createValidator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(new { error = validation.Errors[0].ErrorMessage });

        var result = await _comments.AddAsync(userId.Value, taskId, dto);
        if (!result.Succeeded)
            return BadRequest(new { error = result.Error });

        // Push the new comment to anyone viewing this task in real time.
        await _taskHub.Clients.Group(TaskHub.GroupName(taskId)).SendAsync("ReceiveComment", result.Value);
        return Ok(result.Value);
    }

    /// <summary>Edits a comment the caller authored.</summary>
    [HttpPut("api/tasks/comments/{commentId:guid}")]
    public async Task<IActionResult> Edit(Guid commentId, [FromBody] CreateCommentDto dto)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var validation = await _createValidator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(new { error = validation.Errors[0].ErrorMessage });

        var result = await _comments.EditAsync(userId.Value, commentId, dto.Body);
        if (!result.Succeeded)
            return BadRequest(new { error = result.Error });

        // Push the edit to everyone viewing this task in real time.
        await _taskHub.Clients.Group(TaskHub.GroupName(result.Value!.TaskId)).SendAsync("UpdateComment", result.Value);
        return Ok(result.Value);
    }

    /// <summary>Toggles the caller's emoji reaction on a comment.</summary>
    [HttpPost("api/tasks/comments/{commentId:guid}/react")]
    public async Task<IActionResult> React(Guid commentId, [FromBody] ReactCommentDto dto)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _comments.ToggleReactionAsync(userId.Value, commentId, dto.Emoji);
        if (!result.Succeeded)
            return BadRequest(new { error = result.Error });

        // Push the updated reactions to everyone viewing this task in real time.
        await _taskHub.Clients.Group(TaskHub.GroupName(result.Value!.TaskId))
            .SendAsync("ReceiveCommentReaction", result.Value);
        return Ok(result.Value);
    }

    /// <summary>Deletes a comment the caller authored.</summary>
    [HttpDelete("api/tasks/comments/{commentId:guid}")]
    public async Task<IActionResult> Delete(Guid commentId)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _comments.DeleteAsync(userId.Value, commentId);
        if (!result.Succeeded)
            return BadRequest(new { error = result.Error });

        // Tell other viewers to drop the deleted comment.
        await _taskHub.Clients.Group(TaskHub.GroupName(result.Value)).SendAsync("RemoveComment", commentId);
        return NoContent();
    }
}
