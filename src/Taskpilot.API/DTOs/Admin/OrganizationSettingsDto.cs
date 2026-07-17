namespace Taskpilot.API.DTOs.Admin;

/// <summary>Read model for the organization's settings, shown on the admin settings page.</summary>
public class OrganizationSettingsDto
{
    /// <summary>Largest size, in bytes, a single uploaded file may be.</summary>
    public long MaxUploadBytes { get; set; }

    /// <summary>Total bytes every uploaded file may occupy across the organization.</summary>
    public long StorageQuotaBytes { get; set; }

    /// <summary>Bytes currently used by all stored files — so the admin sees headroom.</summary>
    public long StorageUsedBytes { get; set; }

    /// <summary>Whether the public task Marketplace is available.</summary>
    public bool MarketplaceEnabled { get; set; }

    /// <summary>Whether the discussion Forum is available.</summary>
    public bool ForumEnabled { get; set; }

    /// <summary>UTC time the settings were last changed; null if never edited.</summary>
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>Fields an admin may change on the organization's settings.</summary>
public class UpdateOrganizationSettingsDto
{
    /// <summary>New per-file upload cap, in bytes.</summary>
    public long MaxUploadBytes { get; set; }

    /// <summary>New organization-wide storage quota, in bytes.</summary>
    public long StorageQuotaBytes { get; set; }

    /// <summary>Whether the public task Marketplace is available.</summary>
    public bool MarketplaceEnabled { get; set; }

    /// <summary>Whether the discussion Forum is available.</summary>
    public bool ForumEnabled { get; set; }
}

/// <summary>
/// The subset of settings any signed-in user may read — the feature flags the client uses
/// to hide navigation for disabled features. Deliberately excludes storage limits.
/// </summary>
public class FeatureFlagsDto
{
    public bool MarketplaceEnabled { get; set; }
    public bool ForumEnabled { get; set; }
}
