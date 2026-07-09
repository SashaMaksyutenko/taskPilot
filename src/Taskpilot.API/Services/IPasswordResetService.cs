using Taskpilot.API.Common;

namespace Taskpilot.API.Services;

/// <summary>Handles the forgot-password flow: emailing a reset link and applying the reset.</summary>
public interface IPasswordResetService
{
    /// <summary>
    /// Emails a reset link to the address if an account exists. Always succeeds so
    /// callers can't probe which emails are registered.
    /// </summary>
    Task RequestResetAsync(string email);

    /// <summary>Sets a new password given a valid, unused, unexpired reset token.</summary>
    Task<Result> ResetAsync(string rawToken, string newPassword);
}
