using Taskpilot.API.Common;
using Taskpilot.API.DTOs.Calendar;

namespace Taskpilot.API.Services;

/// <summary>
/// Connects a user's Google Calendar and pushes their deadline tasks to it as events
/// (one-way, TaskPilot → Google, for now — the pull direction is a later slice). Config-gated
/// on Google OAuth: every method no-ops/fails gracefully when it isn't configured.
/// </summary>
public interface IGoogleCalendarSyncService
{
    /// <summary>Reports whether the feature is configured and whether this user is connected.</summary>
    Task<GoogleCalendarStatusDto> GetStatusAsync(Guid userId);

    /// <summary>Builds the Google consent URL the frontend redirects to (offline access for a refresh token).</summary>
    string BuildConnectUrl(string redirectUri, string state);

    /// <summary>Completes the consent flow: exchanges the code and stores the connection.</summary>
    Task<Result> ConnectAsync(Guid userId, string code, string redirectUri);

    /// <summary>Removes the connection (and the task↔event links) for this user.</summary>
    Task<Result> DisconnectAsync(Guid userId);

    /// <summary>Pushes the user's deadline tasks to their Google Calendar (create/update).</summary>
    Task<Result<GoogleCalendarSyncResultDto>> SyncAsync(Guid userId);

    /// <summary>Pulls moved events back: reschedules a task whose Google event start changed (Google → TaskPilot).</summary>
    Task<Result<GoogleCalendarPullResultDto>> PullAsync(Guid userId);
}
