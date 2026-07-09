namespace Taskpilot.API.Models;

/// <summary>
/// One anonymous visitor (by hashed IP) for a given UTC day, with a hit counter.
/// Persisted so visitor analytics survive app restarts. The raw IP is never stored
/// — only its SHA-256 hash — so the table holds no personal data.
/// </summary>
public class VisitorHit
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>UTC day of the visit.</summary>
    public DateOnly Day { get; set; }

    /// <summary>SHA-256 hash of the visitor's IP (or a placeholder when unknown).</summary>
    public string IpHash { get; set; } = string.Empty;

    /// <summary>Number of anonymous requests from this IP on this day.</summary>
    public int Hits { get; set; }
}
