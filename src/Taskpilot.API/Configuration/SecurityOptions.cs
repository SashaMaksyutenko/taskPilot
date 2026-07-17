namespace Taskpilot.API.Configuration;

/// <summary>Security hardening knobs (populated from .env: Security__*).</summary>
public class SecurityOptions
{
    /// <summary>
    /// Comma-separated IPs and/or CIDR ranges allowed to reach the admin API
    /// (e.g. "203.0.113.5, 10.0.0.0/8"). Empty = the allowlist is OFF and the admin API is
    /// reachable from anywhere (it is still behind [Authorize(Roles = "Admin")] either way).
    /// </summary>
    public string AdminIpAllowlist { get; set; } = string.Empty;
}
