using Taskpilot.API.Common;

namespace Taskpilot.API.Services;

/// <summary>Minimal Google account profile returned after an OAuth exchange.</summary>
public record GoogleUserInfo(string Sub, string Email, string Name);

/// <summary>
/// Exchanges a Google OAuth authorization code for the user's profile. The real
/// implementation talks to Google; tests provide a stub so the login logic can be
/// verified without any network calls.
/// </summary>
public interface IGoogleAuthClient
{
    /// <summary>
    /// Exchanges the authorization code for tokens and returns the account profile,
    /// or a failure when the code/credentials are invalid.
    /// </summary>
    Task<Result<GoogleUserInfo>> ExchangeCodeAsync(string code);
}
