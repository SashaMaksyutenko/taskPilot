using Taskpilot.API.Common;

namespace Taskpilot.API.Services;

/// <summary>Viber link status for the current user.</summary>
public record ViberLinkStatus(bool Linked, string BotName);

/// <summary>
/// Links a TaskPilot account to a Viber user via a one-time code: the app shows the
/// user a code, they send it to the bot, and the bot (via its webhook) resolves the
/// code back to the user and stores their Viber id.
/// </summary>
public interface IViberLinkService
{
    /// <summary>Creates a short-lived one-time link code for the user; returns the code and bot name.</summary>
    Task<Result<(string Code, string BotName)>> CreateLinkCodeAsync(Guid userId);

    /// <summary>Resolves a link code and stores the Viber id on the matching user. Returns true when linked.</summary>
    Task<bool> LinkByCodeAsync(string code, string viberId);

    /// <summary>Removes the user's Viber link.</summary>
    Task<Result> UnlinkAsync(Guid userId);

    /// <summary>Returns whether the user has linked Viber (and the bot name).</summary>
    Task<Result<ViberLinkStatus>> GetStatusAsync(Guid userId);
}
