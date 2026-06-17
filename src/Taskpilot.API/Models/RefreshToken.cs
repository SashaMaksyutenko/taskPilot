using System.ComponentModel.DataAnnotations.Schema;

namespace Taskpilot.API.Models;

/// <summary>
/// A refresh token stored in the database. It lets a client obtain a new short-lived
/// access token without re-entering credentials. Refresh tokens are long-lived (7 days)
/// and are rotated on each use (the old one is revoked, a new one is issued).
/// </summary>
public class RefreshToken
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>The random token value sent to and returned by the client.</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>Id of the user this token belongs to (foreign key).</summary>
    public Guid UserId { get; set; }

    /// <summary>Navigation to the owning user.</summary>
    public User User { get; set; } = null!;

    /// <summary>UTC time when the token expires.</summary>
    public DateTime ExpiresAtUtc { get; set; }

    /// <summary>UTC time when the token was created.</summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>UTC time when the token was revoked; null while still valid.</summary>
    public DateTime? RevokedAtUtc { get; set; }

    /// <summary>True once the token has passed its expiry time. Not stored in the DB.</summary>
    [NotMapped]
    public bool IsExpired => DateTime.UtcNow >= ExpiresAtUtc;

    /// <summary>True when the token is neither revoked nor expired. Not stored in the DB.</summary>
    [NotMapped]
    public bool IsActive => RevokedAtUtc is null && !IsExpired;
}
