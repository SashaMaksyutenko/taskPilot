using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Taskpilot.API.Services;

namespace Taskpilot.API.Controllers;

/// <summary>
/// Files attached to a forum topic. The bytes are downloaded through the shared
/// <c>/api/files/{id}</c> endpoint; these routes only manage the link to the topic.
/// </summary>
[ApiController]
[Authorize]
public class ForumAttachmentsController : BaseApiController
{
    private readonly IForumAttachmentService _attachments;

    public ForumAttachmentsController(IForumAttachmentService attachments)
    {
        _attachments = attachments;
    }

    /// <summary>Lists a topic's attachments, newest first.</summary>
    [HttpGet("api/forum/topics/{topicId:guid}/attachments")]
    public async Task<IActionResult> GetForTopic(Guid topicId)
    {
        var result = await _attachments.GetForTopicAsync(topicId);
        return result.Succeeded ? Ok(result.Value) : NotFound(new { error = result.Error });
    }

    /// <summary>Uploads a file and attaches it to the topic (author only).</summary>
    [HttpPost("api/forum/topics/{topicId:guid}/attachments")]
    public async Task<IActionResult> Attach(Guid topicId, IFormFile file)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _attachments.AttachAsync(userId.Value, topicId, file);
        return result.Succeeded
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : BadRequest(new { error = result.Error });
    }

    /// <summary>Removes an attachment and deletes the file behind it (uploader only).</summary>
    [HttpDelete("api/forum/attachments/{attachmentId:guid}")]
    public async Task<IActionResult> Detach(Guid attachmentId)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _attachments.DetachAsync(userId.Value, attachmentId);
        return result.Succeeded ? NoContent() : BadRequest(new { error = result.Error });
    }
}
