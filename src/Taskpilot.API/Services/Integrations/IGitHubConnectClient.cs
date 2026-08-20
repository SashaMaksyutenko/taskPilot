using Taskpilot.API.Common;

namespace Taskpilot.API.Services;

/// <summary>Result of exchanging a GitHub OAuth code: the access token and the linked account.</summary>
public record GitHubTokenResult(string AccessToken, string Login, string? Scope);

/// <summary>A GitHub repository the linked user can access.</summary>
public record GitHubRepo(string FullName, bool Private);

/// <summary>
/// Talks to GitHub for the per-user integration OAuth flow: builds the authorize URL, exchanges the
/// code for a token, and reads the user's repositories. No SDK — raw REST, like the other clients.
/// </summary>
public interface IGitHubConnectClient
{
    /// <summary>True when the integration OAuth app is configured.</summary>
    bool IsEnabled { get; }

    /// <summary>The GitHub authorize URL to redirect the user to (with the given callback + CSRF state).</summary>
    string BuildAuthorizeUrl(string redirectUri, string state);

    /// <summary>Exchanges the returned code for an access token and reads the linked login.</summary>
    Task<Result<GitHubTokenResult>> ExchangeCodeAsync(string code, string redirectUri);

    /// <summary>Lists the repositories the token can access (most-recently-updated first).</summary>
    Task<Result<List<GitHubRepo>>> GetReposAsync(string accessToken);
}
