using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Taskpilot.API.Services;

namespace Taskpilot.API.Controllers;

/// <summary>
/// Files attached to a project task. The bytes are downloaded through the shared
/// <c>/api/files/{id}</c> endpoint; these routes only manage the link to the task.
/// </summary>
[ApiController]
[Authorize]
public class TaskAttachmentsController : BaseApiController
{
    private readonly ITaskAttachmentService _attachments;

    public TaskAttachmentsController(ITaskAttachmentService attachments)
    {
        _attachments = attachments;
    }

    /// <summary>Lists a task's attachments, newest first.</summary>
    [HttpGet("api/tasks/{taskId:guid}/attachments")]
    public async Task<IActionResult> GetForTask(Guid taskId)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _attachments.GetForTaskAsync(userId.Value, taskId);
        return result.Succeeded ? Ok(result.Value) : NotFound(new { error = result.Error });
    }

    /// <summary>Uploads a file and attaches it to the task.</summary>
    [HttpPost("api/tasks/{taskId:guid}/attachments")]
    public async Task<IActionResult> Attach(Guid taskId, IFormFile file)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _attachments.AttachAsync(userId.Value, taskId, file);
        return result.Succeeded
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : BadRequest(new { error = result.Error });
    }

    /// <summary>Removes an attachment and deletes the file behind it (uploader only).</summary>
    [HttpDelete("api/task-attachments/{attachmentId:guid}")]
    public async Task<IActionResult> Detach(Guid attachmentId)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _attachments.DetachAsync(userId.Value, attachmentId);
        return result.Succeeded ? NoContent() : BadRequest(new { error = result.Error });
    }

    /// <summary>Uploads a new version of an attachment, keeping the old one as history (uploader only).</summary>
    [HttpPost("api/task-attachments/{attachmentId:guid}/versions")]
    public async Task<IActionResult> UploadVersion(Guid attachmentId, IFormFile file)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _attachments.UploadVersionAsync(userId.Value, attachmentId, file);
        return result.Succeeded
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : BadRequest(new { error = result.Error });
    }

    /// <summary>Lists an attachment's version history, newest first.</summary>
    [HttpGet("api/task-attachments/{attachmentId:guid}/versions")]
    public async Task<IActionResult> GetVersions(Guid attachmentId)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _attachments.GetVersionsAsync(userId.Value, attachmentId);
        return result.Succeeded ? Ok(result.Value) : NotFound(new { error = result.Error });
    }
}
