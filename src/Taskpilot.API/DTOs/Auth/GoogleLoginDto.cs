namespace Taskpilot.API.DTOs.Auth;

/// <summary>Input for Google sign-in: the one-time authorization code from Google.</summary>
public class GoogleLoginDto
{
    /// <summary>Authorization code returned to the frontend by Google's consent screen.</summary>
    public string Code { get; set; } = string.Empty;
}
