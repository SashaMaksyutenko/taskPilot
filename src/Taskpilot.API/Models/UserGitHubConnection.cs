namespace Taskpilot.API.Models;

/// <summary>
/// A user's linked GitHub account (per-user OAuth integration). Stores the OAuth access token so the
/// app can call the GitHub API on the user's behalf — e.g. list their repositories. One row per user.
/// Mirrors <see cref="GoogleCalendarConnection"/>'s at-rest token handling.
/// </summary>
public class UserGitHubConnection
{
    /// <summary>Primary key and foreign key to the owning user (1:1).</summary>
    public Guid UserId { get; set; }

    /// <summary>Navigation to the user.</summary>
    public User User { get; set; } = null!;

    /// <summary>The GitHub OAuth access token used for API calls on the user's behalf.</summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>The linked GitHub login (username), shown as "Connected as @login".</summary>
    public string Login { get; set; } = string.Empty;

    /// <summary>Scopes granted by the user (from the token exchange).</summary>
    public string? Scope { get; set; }

    /// <summary>When the account was linked.</summary>
    public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;
}
