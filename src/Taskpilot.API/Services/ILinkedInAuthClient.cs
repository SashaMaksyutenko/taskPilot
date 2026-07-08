using Taskpilot.API.Common;

namespace Taskpilot.API.Services;

/// <summary>Minimal LinkedIn account profile returned after an OAuth exchange.</summary>
public record LinkedInUserInfo(string Sub, string Email, string Name);

/// <summary>
/// Exchanges a LinkedIn OAuth authorization code for the user's profile. The real
/// implementation talks to LinkedIn; tests provide a stub so the login logic can be
/// verified without any network calls.
/// </summary>
public interface ILinkedInAuthClient
{
    /// <summary>
    /// Exchanges the authorization code for tokens and returns the account profile,
    /// or a failure when the code/credentials are invalid.
    /// </summary>
    Task<Result<LinkedInUserInfo>> ExchangeCodeAsync(string code);
}
