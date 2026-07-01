namespace Taskpilot.API.Models;

/// <summary>
/// A single-use two-factor recovery code. Only the SHA-256 hash is stored; the plain
/// codes are shown to the user once at generation time.
/// </summary>
public class UserBackupCode
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>User the code belongs to (foreign key).</summary>
    public Guid UserId { get; set; }

    /// <summary>Navigation to the user.</summary>
    public User User { get; set; } = null!;

    /// <summary>SHA-256 hex hash of the normalized code.</summary>
    public string CodeHash { get; set; } = string.Empty;

    /// <summary>UTC time the code was used; null while still valid.</summary>
    public DateTime? UsedAt { get; set; }
}
