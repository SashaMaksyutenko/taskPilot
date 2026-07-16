using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Taskpilot.API.DTOs.ChatBot;
using Taskpilot.API.Services.Assistant;

namespace Taskpilot.API.Controllers;

/// <summary>The in-app AI assistant. All endpoints require authentication.</summary>
[ApiController]
[Authorize]
[Route("api/chatbot")]
public class ChatBotController : BaseApiController
{
    private readonly IAssistantAgent _assistant;

    public ChatBotController(IAssistantAgent assistant)
    {
        _assistant = assistant;
    }

    /// <summary>Whether the assistant is configured and available.</summary>
    [HttpGet("status")]
    public IActionResult Status() => Ok(new { enabled = _assistant.IsEnabled });

    /// <summary>
    /// Answers the user's latest message. The assistant can look up the caller's own
    /// tasks, deadlines and projects via read-only tools before replying.
    /// </summary>
    [HttpPost("ask")]
    public async Task<IActionResult> Ask([FromBody] ChatBotAskDto dto)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _assistant.AskAsync(userId.Value, dto.Messages);
        return result.Succeeded
            ? Ok(new { reply = result.Value })
            : BadRequest(new { error = result.Error });
    }
}
