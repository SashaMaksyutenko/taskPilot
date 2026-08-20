using Taskpilot.API.Common;
using Taskpilot.API.DTOs.Integrations;

namespace Taskpilot.API.Services;

/// <summary>
/// Manages a user's personal GitHub account link (outbound OAuth): connect, disconnect, status, and
/// listing the user's repositories via the stored token. Complements the inbound webhook integration.
/// </summary>
public interface IGitHubConnectionService
{
    /// <summary>Whether the feature is configured and whether this user is linked.</summary>
    Task<GitHubConnectionStatusDto> GetStatusAsync(Guid userId);

    /// <summary>Builds the GitHub authorize URL and remembers the CSRF state for this user.</summary>
    Result<string> BuildConnectUrl(Guid userId, string redirectUri);

    /// <summary>Completes the link with the code GitHub returned (validates the CSRF state).</summary>
    Task<Result> ConnectAsync(Guid userId, string code, string redirectUri, string? state);

    /// <summary>Unlinks the user's GitHub account.</summary>
    Task<Result> DisconnectAsync(Guid userId);

    /// <summary>Lists the repositories the linked account can access.</summary>
    Task<Result<List<GitHubRepoDto>>> GetReposAsync(Guid userId);
}
