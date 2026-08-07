namespace Taskpilot.API.Models;

/// <summary>
/// A WebAuthn / FIDO2 credential ("passkey") a user registered for passwordless sign-in.
/// Stores the credential's public key and signature counter, used to verify assertions.
/// </summary>
public class UserPasskey
{
    public Guid Id { get; set; }

    /// <summary>Owner of the passkey (foreign key).</summary>
    public Guid UserId { get; set; }

    /// <summary>Navigation to the owner.</summary>
    public User User { get; set; } = null!;

    /// <summary>The authenticator's credential id (unique across users).</summary>
    public byte[] CredentialId { get; set; } = Array.Empty<byte>();

    /// <summary>The credential's public key (COSE-encoded).</summary>
    public byte[] PublicKey { get; set; } = Array.Empty<byte>();

    /// <summary>The user handle used at registration (our user id bytes).</summary>
    public byte[] UserHandle { get; set; } = Array.Empty<byte>();

    /// <summary>Signature counter; updated after each assertion (replay/clone detection). Stored as long since Postgres has no uint.</summary>
    public long SignatureCounter { get; set; }

    /// <summary>Credential type (e.g. "public-key").</summary>
    public string CredType { get; set; } = string.Empty;

    /// <summary>Authenticator AAGUID.</summary>
    public Guid Aaguid { get; set; }

    /// <summary>User-facing label for this passkey (e.g. a device name).</summary>
    public string Name { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastUsedAt { get; set; }
}
