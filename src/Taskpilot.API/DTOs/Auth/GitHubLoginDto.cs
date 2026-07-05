namespace Taskpilot.API.DTOs.Auth;

/// <summary>Input for GitHub sign-in: the one-time authorization code from GitHub.</summary>
public class GitHubLoginDto
{
    /// <summary>Authorization code returned to the frontend by GitHub's consent screen.</summary>
    public string Code { get; set; } = string.Empty;
}
