using Taskpilot.API.Models;

namespace Taskpilot.API.Services;

/// <summary>
/// Generates JWT access tokens for authenticated users.
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Creates a signed JWT access token for the given user.
    /// </summary>
    /// <param name="user">The authenticated user the token is issued for.</param>
    /// <returns>The signed token string and its UTC expiry time.</returns>
    (string token, DateTime expiresAtUtc) GenerateAccessToken(User user);
}
