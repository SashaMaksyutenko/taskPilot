using Taskpilot.API.Common;

namespace Taskpilot.API.Services;

/// <summary>Link status for the current user.</summary>
public record TelegramLinkStatus(bool Linked, string BotUsername);

/// <summary>
/// Links a TaskPilot account to a Telegram chat via a one-time code: the app shows
/// the user a code, they send "/start &lt;code&gt;" to the bot, and the bot resolves
/// the code back to the user and stores their chat id.
/// </summary>
public interface ITelegramLinkService
{
    /// <summary>Creates a short-lived one-time link code for the user; returns the code and bot username.</summary>
    Task<Result<(string Code, string BotUsername)>> CreateLinkCodeAsync(Guid userId);

    /// <summary>Resolves a link code and stores the chat id on the matching user. Returns true when linked.</summary>
    Task<bool> LinkByCodeAsync(string code, string chatId);

    /// <summary>Removes the user's Telegram link.</summary>
    Task<Result> UnlinkAsync(Guid userId);

    /// <summary>Returns whether the user has linked Telegram (and the bot username).</summary>
    Task<Result<TelegramLinkStatus>> GetStatusAsync(Guid userId);
}
