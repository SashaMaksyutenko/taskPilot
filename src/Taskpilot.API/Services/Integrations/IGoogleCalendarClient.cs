using Taskpilot.API.Common;

namespace Taskpilot.API.Services;

/// <summary>Tokens returned by Google's OAuth token endpoint.</summary>
public record GoogleTokenResult(string AccessToken, string? RefreshToken, int ExpiresInSeconds);

/// <summary>The data needed to create/update one Google Calendar event for a task.</summary>
public record GoogleCalendarEventData(Guid TaskId, string Title, string Description, DateTime StartUtc, DateTime EndUtc);

/// <summary>
/// Thin client over the Google Calendar API (and its OAuth token endpoint). Config-gated:
/// <see cref="IsEnabled"/> is false unless Google OAuth credentials are configured, so callers
/// can skip the feature gracefully.
/// </summary>
public interface IGoogleCalendarClient
{
    /// <summary>True only when Google OAuth (client id + secret) is configured.</summary>
    bool IsEnabled { get; }

    /// <summary>Swaps a one-time auth code for tokens (expects a refresh token via offline access).</summary>
    Task<Result<GoogleTokenResult>> ExchangeCodeAsync(string code, string redirectUri);

    /// <summary>Mints a fresh access token from a stored refresh token.</summary>
    Task<Result<GoogleTokenResult>> RefreshAccessTokenAsync(string refreshToken);

    /// <summary>Creates (eventId null) or updates an event on the user's primary calendar; returns its id.</summary>
    Task<Result<string>> UpsertEventAsync(string accessToken, string? eventId, GoogleCalendarEventData ev);

    /// <summary>Deletes an event from the user's primary calendar (idempotent — an already-gone event is a success).</summary>
    Task<Result> DeleteEventAsync(string accessToken, string eventId);
}
