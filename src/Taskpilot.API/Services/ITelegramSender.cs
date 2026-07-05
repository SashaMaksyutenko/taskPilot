namespace Taskpilot.API.Services;

/// <summary>
/// Sends messages through the Telegram Bot API. A no-op when no bot token is
/// configured, so callers never need to check first.
/// </summary>
public interface ITelegramSender
{
    /// <summary>True when a bot token is configured.</summary>
    bool IsEnabled { get; }

    /// <summary>Sends a text message to a chat; does nothing (and never throws) when disabled.</summary>
    Task SendMessageAsync(string chatId, string text);
}
