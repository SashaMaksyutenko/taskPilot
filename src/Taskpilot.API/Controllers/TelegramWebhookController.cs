using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Taskpilot.API.DTOs.Integrations;
using Taskpilot.API.Services;
using Taskpilot.Integrations;

namespace Taskpilot.API.Controllers;

/// <summary>
/// Receives Telegram Bot API webhook calls. Anonymous (Telegram has no user token), but guarded
/// by the secret token Telegram echoes in a header — set it when registering the webhook.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/telegram")]
public class TelegramWebhookController : ControllerBase
{
    private const string SecretHeader = "X-Telegram-Bot-Api-Secret-Token";

    private readonly ITelegramUpdateService _updates;
    private readonly TelegramOptions _options;

    public TelegramWebhookController(ITelegramUpdateService updates, IOptions<TelegramOptions> options)
    {
        _updates = updates;
        _options = options.Value;
    }

    /// <summary>Handles one incoming Telegram update. Always returns 200 so Telegram doesn't retry a handled update.</summary>
    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook([FromBody] TelegramUpdate update)
    {
        // When a secret is configured, only accept calls carrying the matching header.
        if (!string.IsNullOrEmpty(_options.WebhookSecret))
        {
            var provided = Request.Headers[SecretHeader].FirstOrDefault();
            if (!string.Equals(provided, _options.WebhookSecret, StringComparison.Ordinal))
                return Unauthorized();
        }

        await _updates.HandleUpdateAsync(update);
        return Ok();
    }
}
