using Taskpilot.API.Common;
using Taskpilot.API.DTOs.ApiKeys;

namespace Taskpilot.API.Services;

/// <summary>The account an API key authenticates as (used to build the request principal).</summary>
public record ApiKeyIdentity(Guid UserId, string Email, string Role);

/// <summary>Manages personal API keys: create, list, revoke, and validate.</summary>
public interface IApiKeyService
{
    /// <summary>Creates a key for the user and returns it once (including the raw secret).</summary>
    Task<Result<CreatedApiKeyDto>> CreateAsync(Guid userId, string name);

    /// <summary>Lists the user's active (non-revoked) keys, newest first.</summary>
    Task<Result<List<ApiKeyDto>>> ListAsync(Guid userId);

    /// <summary>Revokes one of the user's keys.</summary>
    Task<Result> RevokeAsync(Guid userId, Guid keyId);

    /// <summary>
    /// Resolves a raw API key to its owner's identity, or null when unknown/revoked/inactive.
    /// Updates the key's last-used timestamp on a hit.
    /// </summary>
    Task<ApiKeyIdentity?> ResolveAsync(string rawKey);
}
