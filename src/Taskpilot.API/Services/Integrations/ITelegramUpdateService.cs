using Taskpilot.API.DTOs.Integrations;

namespace Taskpilot.API.Services;

/// <summary>
/// Handles incoming Telegram updates (webhook): account linking commands and, for a linked
/// user, routing free-text messages to the AI assistant and replying over Telegram.
/// </summary>
public interface ITelegramUpdateService
{
    /// <summary>Processes one Telegram update and sends any reply. Never throws to the caller.</summary>
    Task HandleUpdateAsync(TelegramUpdate update);
}
