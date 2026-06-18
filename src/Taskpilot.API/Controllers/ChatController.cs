using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Taskpilot.API.DTOs.Chat;
using Taskpilot.API.Services;

namespace Taskpilot.API.Controllers;

/// <summary>
/// REST endpoints for chat: conversations and messages.
/// Every endpoint requires a valid JWT; the acting user is taken from the token.
/// </summary>
[ApiController]
[Authorize]
[Route("api/chat")]
public class ChatController : BaseApiController
{
    private readonly IChatService _chatService;
    private readonly IValidator<SendMessageDto> _sendMessageValidator;
    private readonly IValidator<CreateGroupConversationDto> _createGroupValidator;
    private readonly ILogger<ChatController> _logger;

    public ChatController(
        IChatService chatService,
        IValidator<SendMessageDto> sendMessageValidator,
        IValidator<CreateGroupConversationDto> createGroupValidator,
        ILogger<ChatController> logger)
    {
        _chatService = chatService;
        _sendMessageValidator = sendMessageValidator;
        _createGroupValidator = createGroupValidator;
        _logger = logger;
    }

    /// <summary>Lists all conversations the current user takes part in.</summary>
    [HttpGet("conversations")]
    public async Task<IActionResult> GetConversations()
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _chatService.GetUserConversationsAsync(userId.Value);
        return Ok(result.Value);
    }

    /// <summary>Opens (or creates) a direct conversation with another user.</summary>
    [HttpPost("conversations/direct")]
    public async Task<IActionResult> StartDirect([FromBody] StartDirectConversationDto dto)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _chatService.StartDirectConversationAsync(userId.Value, dto.OtherUserId);
        return result.Succeeded
            ? Ok(result.Value)
            : BadRequest(new { error = result.Error });
    }

    /// <summary>Creates a group conversation.</summary>
    [HttpPost("conversations/group")]
    public async Task<IActionResult> CreateGroup([FromBody] CreateGroupConversationDto dto)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var validation = await _createGroupValidator.ValidateAsync(dto);
        if (!validation.IsValid)
        {
            var errors = validation.Errors.Select(e => new { field = e.PropertyName, message = e.ErrorMessage });
            return BadRequest(new { errors });
        }

        var result = await _chatService.CreateGroupConversationAsync(userId.Value, dto);
        return result.Succeeded
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : BadRequest(new { error = result.Error });
    }

    /// <summary>Returns the messages of a conversation the current user belongs to.</summary>
    [HttpGet("conversations/{conversationId:guid}/messages")]
    public async Task<IActionResult> GetMessages(Guid conversationId)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _chatService.GetMessagesAsync(conversationId, userId.Value);
        // Not a participant -> 403 Forbidden.
        return result.Succeeded
            ? Ok(result.Value)
            : StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error });
    }

    /// <summary>Sends a new message to a conversation the current user belongs to.</summary>
    [HttpPost("messages")]
    public async Task<IActionResult> SendMessage([FromBody] SendMessageDto dto)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var validation = await _sendMessageValidator.ValidateAsync(dto);
        if (!validation.IsValid)
        {
            var errors = validation.Errors.Select(e => new { field = e.PropertyName, message = e.ErrorMessage });
            return BadRequest(new { errors });
        }

        var result = await _chatService.SendMessageAsync(userId.Value, dto);
        // A non-participant is forbidden; other failures are bad requests.
        if (!result.Succeeded)
            return StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error });

        return StatusCode(StatusCodes.Status201Created, result.Value);
    }
}
