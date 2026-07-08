using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Taskpilot.API.DTOs.ChatBot;
using Taskpilot.API.Services;

namespace Taskpilot.API.Controllers;

/// <summary>The in-app AI assistant. All endpoints require authentication.</summary>
[ApiController]
[Authorize]
[Route("api/chatbot")]
public class ChatBotController : BaseApiController
{
    private readonly IChatBotService _chatBot;

    public ChatBotController(IChatBotService chatBot)
    {
        _chatBot = chatBot;
    }

    /// <summary>Whether the assistant is configured and available.</summary>
    [HttpGet("status")]
    public IActionResult Status() => Ok(new { enabled = _chatBot.IsEnabled });

    /// <summary>Answers the user's latest message given the running conversation.</summary>
    [HttpPost("ask")]
    public async Task<IActionResult> Ask([FromBody] ChatBotAskDto dto)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _chatBot.AskAsync(dto.Messages);
        return result.Succeeded
            ? Ok(new { reply = result.Value })
            : BadRequest(new { error = result.Error });
    }
}
