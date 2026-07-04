namespace Taskpilot.API.Configuration;

/// <summary>
/// Google OAuth 2.0 credentials, bound from configuration (section "GoogleOAuth").
/// Secrets come from .env / User Secrets — never hard-coded.
/// </summary>
public class GoogleOAuthOptions
{
    /// <summary>OAuth client id from the Google Cloud Console.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>OAuth client secret (kept out of source control).</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Redirect URI registered with Google; must match the frontend callback.</summary>
    public string RedirectUri { get; set; } = string.Empty;

    /// <summary>True only when a client id and secret are configured.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
}
