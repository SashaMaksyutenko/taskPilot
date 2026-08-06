namespace Taskpilot.API.Models;

/// <summary>
/// A user's connection to their Google Calendar (one per user). Holds the long-lived refresh
/// token (used to mint short-lived access tokens) so TaskPilot can push their deadline tasks
/// as calendar events. Exists only after the user completes Google's consent flow; removed on
/// disconnect. Tokens are secrets — never returned to the client.
/// </summary>
public class GoogleCalendarConnection
{
    public Guid Id { get; set; }

    /// <summary>The user who connected their calendar (foreign key, unique).</summary>
    public Guid UserId { get; set; }

    /// <summary>Navigation to the owner.</summary>
    public User User { get; set; } = null!;

    /// <summary>Long-lived OAuth refresh token (Google offline access).</summary>
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>Cached short-lived access token, refreshed on demand.</summary>
    public string? AccessToken { get; set; }

    /// <summary>UTC expiry of <see cref="AccessToken"/>; refreshed when close.</summary>
    public DateTime? AccessTokenExpiresUtc { get; set; }

    /// <summary>UTC time of the last successful push, if any.</summary>
    public DateTime? LastSyncedUtc { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
