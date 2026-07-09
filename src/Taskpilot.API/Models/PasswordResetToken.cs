namespace Taskpilot.API.Models;

/// <summary>
/// A one-time password-reset token. The raw token is emailed to the user; only its
/// SHA-256 hash is stored. Single-use and short-lived.
/// </summary>
public class PasswordResetToken
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>User the token resets the password for.</summary>
    public Guid UserId { get; set; }

    /// <summary>Navigation to the user.</summary>
    public User User { get; set; } = null!;

    /// <summary>SHA-256 hash (hex) of the raw token. The raw token is never stored.</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>UTC time the token expires.</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>UTC time the token was used; null while still valid.</summary>
    public DateTime? UsedAt { get; set; }

    /// <summary>UTC time the token was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
