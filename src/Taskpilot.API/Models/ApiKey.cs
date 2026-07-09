namespace Taskpilot.API.Models;

/// <summary>
/// A personal API key for programmatic access. The raw key is shown to the user
/// once at creation; only its SHA-256 hash is stored. The short prefix is kept in
/// clear so the user can recognise a key in the list.
/// </summary>
public class ApiKey
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Owner of the key.</summary>
    public Guid UserId { get; set; }

    /// <summary>Navigation to the owner.</summary>
    public User User { get; set; } = null!;

    /// <summary>User-chosen label (e.g. "CI pipeline").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>SHA-256 hash (hex) of the raw key. The raw key is never stored.</summary>
    public string KeyHash { get; set; } = string.Empty;

    /// <summary>First few characters of the raw key, shown so the user can identify it.</summary>
    public string Prefix { get; set; } = string.Empty;

    /// <summary>UTC time the key was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>UTC time the key was last used to authenticate; null if never used.</summary>
    public DateTime? LastUsedAt { get; set; }

    /// <summary>UTC time the key was revoked; null while active.</summary>
    public DateTime? RevokedAt { get; set; }
}
