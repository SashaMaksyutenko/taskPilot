namespace Taskpilot.API.Configuration;

/// <summary>
/// LinkedIn OAuth 2.0 (OpenID Connect) credentials, bound from the "LinkedInOAuth"
/// section. Secrets come from .env / User Secrets — never hard-coded.
/// </summary>
public class LinkedInOAuthOptions
{
    /// <summary>OAuth client id from the LinkedIn developer app.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>OAuth client secret (kept out of source control).</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Redirect URI registered with LinkedIn; must match the frontend callback.</summary>
    public string RedirectUri { get; set; } = string.Empty;

    /// <summary>True only when a client id and secret are configured.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
}
