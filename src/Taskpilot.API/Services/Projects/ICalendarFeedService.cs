namespace Taskpilot.API.Services;

/// <summary>
/// Manages the per-user secret token behind the private iCal subscription feed.
/// The token lives in the feed URL so calendar apps (which cannot send auth headers)
/// can poll it; regenerating the token invalidates the old URL.
/// </summary>
public interface ICalendarFeedService
{
    /// <summary>Returns the user's feed token, creating one on first use.</summary>
    Task<string> GetOrCreateTokenAsync(Guid userId);

    /// <summary>Replaces the user's feed token (invalidates the previous URL).</summary>
    Task<string> RegenerateTokenAsync(Guid userId);

    /// <summary>Resolves a feed token back to its user id, or null when unknown.</summary>
    Task<Guid?> ResolveUserIdAsync(string token);
}
