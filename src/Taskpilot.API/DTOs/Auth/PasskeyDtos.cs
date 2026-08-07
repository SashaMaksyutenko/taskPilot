using Fido2NetLib;

namespace Taskpilot.API.DTOs.Auth;

/// <summary>A registered passkey, for listing in settings.</summary>
public class PasskeyDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
}

/// <summary>Body to finish registering a passkey (the authenticator's attestation + a device label).</summary>
public class PasskeyRegisterCompleteDto
{
    public AuthenticatorAttestationRawResponse AttestationResponse { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
}

/// <summary>Body to request assertion options for sign-in.</summary>
public class PasskeyLoginOptionsDto
{
    public string Email { get; set; } = string.Empty;
}

/// <summary>Assertion options plus the ceremony id the client echoes back on completion.</summary>
public class PasskeyLoginOptionsResponseDto
{
    public string CeremonyId { get; set; } = string.Empty;

    /// <summary>The assertion options as a WebAuthn-shaped JSON string; the client JSON.parses it.</summary>
    public string OptionsJson { get; set; } = string.Empty;
}

/// <summary>Body to finish signing in with a passkey.</summary>
public class PasskeyLoginCompleteDto
{
    public string CeremonyId { get; set; } = string.Empty;
    public AuthenticatorAssertionRawResponse AssertionResponse { get; set; } = null!;
}
