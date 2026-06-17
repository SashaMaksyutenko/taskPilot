namespace Taskpilot.API.DTOs.Auth;

/// <summary>
/// Input model for the login endpoint (POST /api/auth/login).
/// Carries the credentials sent by the client. Validated by
/// <see cref="Taskpilot.API.Validators.Auth.LoginValidator"/> before any
/// authentication logic runs.
/// </summary>
public class LoginDto
{
    /// <summary>Email address used as the login.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Plain-text password sent by the client. It is compared against the stored
    /// BCrypt hash during login and is never persisted or logged.
    /// </summary>
    public string Password { get; set; } = string.Empty;
}
