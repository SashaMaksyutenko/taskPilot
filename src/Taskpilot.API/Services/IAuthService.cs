using Taskpilot.API.Common;
using Taskpilot.API.DTOs.Auth;

namespace Taskpilot.API.Services;

/// <summary>
/// Authentication business logic (registration, login, tokens).
/// At this stage only registration is implemented; login/JWT come in later sessions.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Registers a new user: checks the email is unique, hashes the password
    /// and persists the user.
    /// </summary>
    /// <param name="dto">Validated registration data (name, email, password).</param>
    /// <returns>
    /// A successful result with the new user's Id, or a failed result
    /// (for example when the email is already in use).
    /// </returns>
    Task<Result<Guid>> RegisterAsync(RegisterDto dto);
}
