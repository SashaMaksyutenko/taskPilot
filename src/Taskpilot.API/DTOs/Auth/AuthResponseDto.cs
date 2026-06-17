namespace Taskpilot.API.DTOs.Auth;

/// <summary>
/// Response returned after a successful login.
/// Contains the access token and minimal, non-sensitive user info.
/// The password hash is never included.
/// </summary>
public class AuthResponseDto
{
    /// <summary>Signed JWT access token to send in the Authorization header.</summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>UTC time when the access token expires.</summary>
    public DateTime ExpiresAtUtc { get; set; }

    /// <summary>Long-lived refresh token used to obtain new access tokens.</summary>
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>UTC time when the refresh token expires.</summary>
    public DateTime RefreshTokenExpiresAtUtc { get; set; }

    /// <summary>Id of the authenticated user.</summary>
    public Guid UserId { get; set; }

    /// <summary>Email of the authenticated user.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Role of the authenticated user (e.g. "Developer").</summary>
    public string Role { get; set; } = string.Empty;
}
