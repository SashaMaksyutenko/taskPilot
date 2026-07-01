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

    /// <summary>
    /// Authenticates a user by email and password and, on success, issues a JWT.
    /// </summary>
    /// <param name="dto">Validated login data (email, password).</param>
    /// <returns>
    /// A successful result with the access token and user info, or a failed
    /// result with a generic message when the credentials are invalid.
    /// </returns>
    Task<Result<AuthResponseDto>> LoginAsync(LoginDto dto, string? ip = null, string? userAgent = null);

    /// <summary>
    /// Exchanges a valid refresh token for a new access token and a rotated
    /// refresh token (the used one is revoked).
    /// </summary>
    /// <param name="refreshToken">The refresh token previously issued to the client.</param>
    /// <returns>
    /// A successful result with fresh tokens, or a failed result when the refresh
    /// token is missing, expired or already revoked.
    /// </returns>
    Task<Result<AuthResponseDto>> RefreshAsync(string refreshToken, string? ip = null, string? userAgent = null);

    /// <summary>Lists the user's active sessions; the one matching currentToken is flagged.</summary>
    Task<Result<List<SessionDto>>> GetSessionsAsync(Guid userId, string? currentToken);

    /// <summary>Revokes one of the user's sessions by id.</summary>
    Task<Result> RevokeSessionAsync(Guid userId, Guid sessionId);

    /// <summary>Revokes all of the user's sessions except the one matching currentToken.</summary>
    Task<Result> RevokeOtherSessionsAsync(Guid userId, string? currentToken);

    /// <summary>
    /// Returns the public profile of the user with the given id.
    /// </summary>
    /// <param name="userId">Id taken from the authenticated JWT.</param>
    /// <returns>Success with the user profile, or failure when the user no longer exists.</returns>
    Task<Result<UserDto>> GetCurrentUserAsync(Guid userId);
}
