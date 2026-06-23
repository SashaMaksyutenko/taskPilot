using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Taskpilot.API.DTOs.Forum;
using Taskpilot.API.Services;

namespace Taskpilot.API.Controllers;

/// <summary>
/// REST endpoints for the forum: topics and replies. All require authentication.
/// </summary>
[ApiController]
[Authorize]
[Route("api/forum")]
public class ForumController : BaseApiController
{
    private readonly IForumService _forumService;
    private readonly IValidator<CreateTopicDto> _createTopicValidator;
    private readonly IValidator<CreateReplyDto> _createReplyValidator;

    public ForumController(
        IForumService forumService,
        IValidator<CreateTopicDto> createTopicValidator,
        IValidator<CreateReplyDto> createReplyValidator)
    {
        _forumService = forumService;
        _createTopicValidator = createTopicValidator;
        _createReplyValidator = createReplyValidator;
    }

    /// <summary>Lists topics (pinned first, then newest), optionally filtered by author.</summary>
    [HttpGet("topics")]
    public async Task<IActionResult> GetTopics([FromQuery] Guid? authorId)
    {
        var result = await _forumService.GetTopicsAsync(authorId);
        return Ok(result.Value);
    }

    /// <summary>Returns a topic with its replies (and counts the view).</summary>
    [HttpGet("topics/{topicId:guid}")]
    public async Task<IActionResult> GetTopic(Guid topicId)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _forumService.GetTopicAsync(topicId, userId.Value);
        return result.Succeeded
            ? Ok(result.Value)
            : NotFound(new { error = result.Error });
    }

    /// <summary>Creates a new topic.</summary>
    [HttpPost("topics")]
    public async Task<IActionResult> CreateTopic([FromBody] CreateTopicDto dto)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var validation = await _createTopicValidator.ValidateAsync(dto);
        if (!validation.IsValid)
        {
            var errors = validation.Errors.Select(e => new { field = e.PropertyName, message = e.ErrorMessage });
            return BadRequest(new { errors });
        }

        var result = await _forumService.CreateTopicAsync(userId.Value, dto);
        return result.Succeeded
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : BadRequest(new { error = result.Error });
    }

    /// <summary>Posts a reply to a topic.</summary>
    [HttpPost("replies")]
    public async Task<IActionResult> AddReply([FromBody] CreateReplyDto dto)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var validation = await _createReplyValidator.ValidateAsync(dto);
        if (!validation.IsValid)
        {
            var errors = validation.Errors.Select(e => new { field = e.PropertyName, message = e.ErrorMessage });
            return BadRequest(new { errors });
        }

        var result = await _forumService.AddReplyAsync(userId.Value, dto);
        return result.Succeeded
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : BadRequest(new { error = result.Error });
    }

    /// <summary>Upvotes or downvotes a reply (value +1 or -1; same value again removes it).</summary>
    [HttpPost("replies/{replyId:guid}/vote")]
    public async Task<IActionResult> Vote(Guid replyId, [FromBody] VoteDto dto)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _forumService.VoteReplyAsync(userId.Value, replyId, dto.Value);
        return result.Succeeded
            ? Ok(result.Value)
            : BadRequest(new { error = result.Error });
    }

    /// <summary>Marks a reply as the accepted solution (topic author only).</summary>
    [HttpPost("replies/{replyId:guid}/solution")]
    public async Task<IActionResult> MarkSolution(Guid replyId)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _forumService.MarkSolutionAsync(userId.Value, replyId);
        return result.Succeeded
            ? Ok(new { message = "Solution marked." })
            : BadRequest(new { error = result.Error });
    }
}
