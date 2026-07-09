namespace Taskpilot.API.DTOs.Auth;

/// <summary>Request a password-reset email for the given address.</summary>
public class ForgotPasswordDto
{
    public string Email { get; set; } = string.Empty;
}

/// <summary>Complete a password reset with the emailed token and a new password.</summary>
public class ResetPasswordDto
{
    public string Token { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}
