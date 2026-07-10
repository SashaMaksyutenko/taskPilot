using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Taskpilot.API.DTOs.Chat;
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
    private readonly IValidator<EditReplyDto> _editReplyValidator;
    private readonly IValidator<EditTopicDto> _editTopicValidator;

    public ForumController(
        IForumService forumService,
        IValidator<CreateTopicDto> createTopicValidator,
        IValidator<CreateReplyDto> createReplyValidator,
        IValidator<EditReplyDto> editReplyValidator,
        IValidator<EditTopicDto> editTopicValidator)
    {
        _forumService = forumService;
        _createTopicValidator = createTopicValidator;
        _createReplyValidator = createReplyValidator;
        _editReplyValidator = editReplyValidator;
        _editTopicValidator = editTopicValidator;
    }

    /// <summary>
    /// Lists a page of topics (pinned first), optionally filtered by author, a search
    /// term and solved status, and ordered by "latest", "active" or "top".
    /// </summary>
    [HttpGet("topics")]
    public async Task<IActionResult> GetTopics(
        [FromQuery] Guid? authorId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] bool? solved = null,
        [FromQuery] string? sort = null)
    {
        var result = await _forumService.GetTopicsAsync(authorId, page, pageSize, search, solved, sort);
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

    /// <summary>Counts one view of a topic. Called once when the topic page opens.</summary>
    [HttpPost("topics/{topicId:guid}/view")]
    public async Task<IActionResult> IncrementView(Guid topicId)
    {
        var result = await _forumService.IncrementViewAsync(topicId);
        return result.Succeeded ? Ok() : NotFound(new { error = result.Error });
    }

    /// <summary>Edits a topic's title and body (author or admin only).</summary>
    [HttpPut("topics/{topicId:guid}")]
    public async Task<IActionResult> EditTopic(Guid topicId, [FromBody] EditTopicDto dto)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var validation = await _editTopicValidator.ValidateAsync(dto);
        if (!validation.IsValid)
        {
            var errors = validation.Errors.Select(e => new { field = e.PropertyName, message = e.ErrorMessage });
            return BadRequest(new { errors });
        }

        var result = await _forumService.EditTopicAsync(userId.Value, topicId, dto.Title, dto.Body, User.IsInRole("Admin"));
        return result.Succeeded
            ? Ok(result.Value)
            : BadRequest(new { error = result.Error });
    }

    /// <summary>Pins or unpins a topic (admin only).</summary>
    [HttpPost("topics/{topicId:guid}/pin")]
    public async Task<IActionResult> PinTopic(Guid topicId, [FromBody] SetFlagDto dto)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _forumService.SetTopicPinnedAsync(topicId, userId.Value, dto.Value, User.IsInRole("Admin"));
        return result.Succeeded
            ? Ok(new { pinned = dto.Value })
            : BadRequest(new { error = result.Error });
    }

    /// <summary>Locks or unlocks a topic (admin or topic author).</summary>
    [HttpPost("topics/{topicId:guid}/lock")]
    public async Task<IActionResult> LockTopic(Guid topicId, [FromBody] SetFlagDto dto)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _forumService.SetTopicLockedAsync(topicId, userId.Value, dto.Value, User.IsInRole("Admin"));
        return result.Succeeded
            ? Ok(new { locked = dto.Value })
            : BadRequest(new { error = result.Error });
    }

    /// <summary>Toggles the current user's subscription to a topic.</summary>
    [HttpPost("topics/{topicId:guid}/subscribe")]
    public async Task<IActionResult> Subscribe(Guid topicId)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _forumService.ToggleSubscriptionAsync(topicId, userId.Value);
        return result.Succeeded
            ? Ok(new { subscribed = result.Value })
            : BadRequest(new { error = result.Error });
    }

    /// <summary>Deletes a topic (author or admin only).</summary>
    [HttpDelete("topics/{topicId:guid}")]
    public async Task<IActionResult> DeleteTopic(Guid topicId)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _forumService.DeleteTopicAsync(topicId, userId.Value, User.IsInRole("Admin"));
        return result.Succeeded
            ? Ok(new { message = "Topic deleted." })
            : BadRequest(new { error = result.Error });
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

    /// <summary>Edits a reply's text (author or admin only).</summary>
    [HttpPut("replies/{replyId:guid}")]
    public async Task<IActionResult> EditReply(Guid replyId, [FromBody] EditReplyDto dto)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var validation = await _editReplyValidator.ValidateAsync(dto);
        if (!validation.IsValid)
        {
            var errors = validation.Errors.Select(e => new { field = e.PropertyName, message = e.ErrorMessage });
            return BadRequest(new { errors });
        }

        var result = await _forumService.EditReplyAsync(userId.Value, replyId, dto.Body, User.IsInRole("Admin"));
        return result.Succeeded
            ? Ok(result.Value)
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

    /// <summary>Toggles the current user's emoji reaction on a reply.</summary>
    [HttpPost("replies/{replyId:guid}/reactions")]
    public async Task<IActionResult> ReactToReply(Guid replyId, [FromBody] ReactDto dto)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _forumService.ToggleReplyReactionAsync(userId.Value, replyId, dto.Emoji);
        return result.Succeeded
            ? Ok(result.Value)
            : BadRequest(new { error = result.Error });
    }

    /// <summary>Soft-deletes a reply (author or admin only).</summary>
    [HttpDelete("replies/{replyId:guid}")]
    public async Task<IActionResult> DeleteReply(Guid replyId)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _forumService.DeleteReplyAsync(userId.Value, replyId, User.IsInRole("Admin"));
        return result.Succeeded
            ? Ok(new { message = "Reply deleted." })
            : BadRequest(new { error = result.Error });
    }
}
