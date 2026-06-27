using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Taskpilot.API.DTOs.Projects;
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

    public TaskCommentsController(ITaskCommentService comments, IValidator<CreateCommentDto> createValidator)
    {
        _comments = comments;
        _createValidator = createValidator;
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
        return result.Succeeded
            ? Ok(result.Value)
            : BadRequest(new { error = result.Error });
    }

    /// <summary>Deletes a comment the caller authored.</summary>
    [HttpDelete("api/tasks/comments/{commentId:guid}")]
    public async Task<IActionResult> Delete(Guid commentId)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _comments.DeleteAsync(userId.Value, commentId);
        return result.Succeeded
            ? NoContent()
            : BadRequest(new { error = result.Error });
    }
}
