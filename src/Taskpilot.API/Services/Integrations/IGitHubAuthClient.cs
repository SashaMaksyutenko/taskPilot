using Taskpilot.API.Common;

namespace Taskpilot.API.Services;

/// <summary>Minimal GitHub account profile returned after an OAuth exchange.</summary>
public record GitHubUserInfo(string Id, string Email, string Name);

/// <summary>
/// Exchanges a GitHub OAuth authorization code for the user's profile. The real
/// implementation talks to GitHub; tests provide a stub so the login logic can be
/// verified without any network calls.
/// </summary>
public interface IGitHubAuthClient
{
    /// <summary>
    /// Exchanges the authorization code for a token and returns the account profile,
    /// or a failure when the code/credentials are invalid.
    /// </summary>
    Task<Result<GitHubUserInfo>> ExchangeCodeAsync(string code);
}
