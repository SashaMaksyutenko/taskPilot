namespace Taskpilot.API.DTOs.Auth;

/// <summary>
/// Input model for the user registration endpoint (POST /api/auth/register).
/// Carries the raw data sent by the client. It is validated by
/// <see cref="Taskpilot.API.Validators.Auth.RegisterValidator"/> before any
/// business logic runs. Never trust these values until they are validated.
/// </summary>
public class RegisterDto
{
    /// <summary>Display name of the new user.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Email address — used as the login and must be unique.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Plain-text password sent by the client. It is validated here, then
    /// hashed with BCrypt before storage — the raw value is never persisted or logged.
    /// </summary>
    public string Password { get; set; } = string.Empty;
}
