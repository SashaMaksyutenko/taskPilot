namespace Taskpilot.API.Configuration;

/// <summary>
/// Credentials for the per-user GitHub *integration* link (separate from GitHub sign-in). This is a
/// dedicated OAuth app requesting the <c>repo</c> scope, so a user can connect their account and the
/// app can read their repositories on their behalf. Bound from the "GitHubIntegration" section;
/// secrets come from .env / User Secrets. The callback URL is supplied by the frontend per request
/// (like Google Calendar), so the same app works across local/preview/prod.
/// </summary>
public class GitHubIntegrationOptions
{
    /// <summary>OAuth app client id (GitHub Developer settings → OAuth Apps).</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>OAuth app client secret.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>OAuth scopes to request. Defaults to <c>repo</c> so we can list the user's repos.</summary>
    public string Scope { get; set; } = "repo";

    /// <summary>True only when both a client id and secret are configured.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
}
