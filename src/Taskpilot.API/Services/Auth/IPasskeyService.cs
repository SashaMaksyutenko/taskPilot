using Fido2NetLib;
using Taskpilot.API.Common;
using Taskpilot.API.DTOs.Auth;

namespace Taskpilot.API.Services;

/// <summary>
/// WebAuthn / FIDO2 passkeys: registration and passwordless sign-in ceremonies, plus
/// listing and removing a user's registered passkeys.
/// </summary>
public interface IPasskeyService
{
    /// <summary>Creation options (a challenge) for registering a new passkey for the user.</summary>
    Task<Result<CredentialCreateOptions>> GetRegisterOptionsAsync(Guid userId);

    /// <summary>Verifies the authenticator's attestation and stores the new passkey.</summary>
    Task<Result> CompleteRegisterAsync(Guid userId, PasskeyRegisterCompleteDto dto);

    /// <summary>The user's registered passkeys.</summary>
    Task<Result<List<PasskeyDto>>> ListAsync(Guid userId);

    /// <summary>Removes one of the user's passkeys.</summary>
    Task<Result> DeleteAsync(Guid userId, Guid passkeyId);

    /// <summary>Assertion options (a challenge) for signing in with the account's passkeys.</summary>
    Task<Result<PasskeyLoginOptionsResponseDto>> GetLoginOptionsAsync(string email);

    /// <summary>Verifies a passkey assertion and issues auth tokens on success.</summary>
    Task<Result<AuthResponseDto>> CompleteLoginAsync(PasskeyLoginCompleteDto dto, string? ip, string? userAgent);
}
