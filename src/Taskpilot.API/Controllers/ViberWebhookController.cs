using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Taskpilot.API.Services;

namespace Taskpilot.API.Controllers;

/// <summary>
/// Receives callbacks from the Viber Bot API. Viber has no polling API, so linking
/// works through this public webhook: when a user sends the bot their one-time code,
/// Viber POSTs it here and we link their account.
/// Register the URL once via Viber's set_webhook with this endpoint (needs a public host).
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/viber")]
public class ViberWebhookController : ControllerBase
{
    private readonly IViberLinkService _link;
    private readonly IViberSender _sender;
    private readonly ILogger<ViberWebhookController> _logger;

    public ViberWebhookController(IViberLinkService link, IViberSender sender, ILogger<ViberWebhookController> logger)
    {
        _link = link;
        _sender = sender;
        _logger = logger;
    }

    /// <summary>Viber event callback (message, conversation_started, etc.).</summary>
    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook([FromBody] JsonElement body)
    {
        var eventType = body.TryGetProperty("event", out var ev) ? ev.GetString() : null;

        switch (eventType)
        {
            case "conversation_started":
                // The user opened the chat; prompt them to send their link code.
                if (TryGetString(body, "user", "id", out var userId))
                    await _sender.SendMessageAsync(userId!,
                        "Welcome to TaskPilot! Send the code shown in Settings to link your account.");
                break;

            case "message":
                await HandleMessageAsync(body);
                break;

            // subscribed / unsubscribed / delivered / seen / webhook — nothing to do.
        }

        return Ok();
    }

    private async Task HandleMessageAsync(JsonElement body)
    {
        if (!TryGetString(body, "sender", "id", out var senderId) || string.IsNullOrEmpty(senderId))
            return;

        var text = body.TryGetProperty("message", out var msg) && msg.TryGetProperty("text", out var txt)
            ? txt.GetString()
            : null;
        if (string.IsNullOrWhiteSpace(text))
            return;

        // Accept the last whitespace-separated token as the one-time link code.
        var code = text.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        if (string.IsNullOrEmpty(code))
            return;

        var linked = await _link.LinkByCodeAsync(code, senderId!);
        await _sender.SendMessageAsync(senderId!, linked
            ? "✅ Your Viber is now linked to TaskPilot. You'll get notifications here."
            : "That code is invalid or expired. Open Settings in TaskPilot to get a new one.");

        _logger.LogInformation("Viber link attempt via webhook. Linked: {Linked}", linked);
    }

    // Reads body[outer][inner] as a string when both properties exist.
    private static bool TryGetString(JsonElement body, string outer, string inner, out string? value)
    {
        value = null;
        if (body.TryGetProperty(outer, out var o) && o.ValueKind == JsonValueKind.Object
            && o.TryGetProperty(inner, out var i))
        {
            value = i.GetString();
            return value is not null;
        }
        return false;
    }
}
