namespace Taskpilot.API.Configuration;

/// <summary>
/// GitHub OAuth credentials, bound from configuration (section "GitHubOAuth").
/// Secrets come from .env / User Secrets — never hard-coded.
/// </summary>
public class GitHubOAuthOptions
{
    /// <summary>OAuth app client id from GitHub Developer settings.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>OAuth app client secret (kept out of source control).</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Redirect URI registered with GitHub; must match the frontend callback.</summary>
    public string RedirectUri { get; set; } = string.Empty;

    /// <summary>True only when a client id and secret are configured.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
}
