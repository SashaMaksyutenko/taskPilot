namespace Taskpilot.API.DTOs.Auth;

/// <summary>Input for LinkedIn sign-in: the one-time authorization code from LinkedIn.</summary>
public class LinkedInLoginDto
{
    /// <summary>Authorization code returned to the frontend by LinkedIn's consent screen.</summary>
    public string Code { get; set; } = string.Empty;
}
