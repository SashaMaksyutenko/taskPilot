namespace Taskpilot.API.DTOs.Admin;

/// <summary>Read model for the organization's settings, shown on the admin settings page.</summary>
public class OrganizationSettingsDto
{
    /// <summary>Organization name shown across the app.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>URL of the custom logo, or null when the built-in logo is used.</summary>
    public string? LogoUrl { get; set; }

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

    /// <summary>Comma-separated email domains allowed to register; empty means any domain.</summary>
    public string AllowedEmailDomains { get; set; } = string.Empty;

    /// <summary>Comma-separated email domains barred from registering; empty blocks nothing.</summary>
    public string BlockedEmailDomains { get; set; } = string.Empty;

    /// <summary>Largest number of active accounts allowed; 0 means unlimited.</summary>
    public int MaxMembers { get; set; }

    /// <summary>How many active accounts exist right now — so the admin sees the headroom.</summary>
    public int ActiveMembers { get; set; }

    /// <summary>UTC time the settings were last changed; null if never edited.</summary>
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Storage limits an admin may change. Separate from the feature flags so each update
/// touches only its own fields — the settings PUT is not a whole-record replace, so
/// changing limits can never reset the feature flags (or vice versa).
/// </summary>
public class UpdateStorageDto
{
    /// <summary>New per-file upload cap, in bytes.</summary>
    public long MaxUploadBytes { get; set; }

    /// <summary>New organization-wide storage quota, in bytes.</summary>
    public long StorageQuotaBytes { get; set; }
}

/// <summary>General organization details an admin may change (independent of the other groups).</summary>
public class UpdateGeneralDto
{
    /// <summary>New organization name. Required; trimmed before storage.</summary>
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// The organization's public branding — the name shown before sign-in (login/landing).
/// Deliberately tiny: it is served to anonymous callers, so it exposes nothing sensitive.
/// </summary>
public class OrganizationBrandingDto
{
    public string Name { get; set; } = string.Empty;

    /// <summary>URL of the custom logo, or null when the built-in logo should be shown.</summary>
    public string? LogoUrl { get; set; }
}

/// <summary>Feature flags an admin may change (independent of the storage limits).</summary>
public class UpdateFeaturesDto
{
    /// <summary>Whether the public task Marketplace is available.</summary>
    public bool MarketplaceEnabled { get; set; }

    /// <summary>Whether the discussion Forum is available.</summary>
    public bool ForumEnabled { get; set; }
}

/// <summary>Registration controls an admin may change (independent of storage and features).</summary>
public class UpdateRegistrationDto
{
    /// <summary>
    /// Comma-separated email domains allowed to self-register. Empty opens registration to
    /// any domain.
    /// </summary>
    public string AllowedEmailDomains { get; set; } = string.Empty;

    /// <summary>
    /// Comma-separated email domains barred from registering. Empty blocks nothing.
    /// Applied before the allowlist.
    /// </summary>
    public string BlockedEmailDomains { get; set; } = string.Empty;

    /// <summary>Largest number of active accounts allowed; 0 (or negative) means unlimited.</summary>
    public int MaxMembers { get; set; }
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
