namespace Taskpilot.API.DTOs.Auth;

/// <summary>
/// Input model for the refresh endpoint (POST /api/auth/refresh).
/// Carries the refresh token the client received at login.
/// </summary>
public class RefreshTokenDto
{
    /// <summary>The refresh token previously issued to the client.</summary>
    public string RefreshToken { get; set; } = string.Empty;
}
