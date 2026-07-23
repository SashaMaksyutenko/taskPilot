using Taskpilot.API.Common;
using Taskpilot.API.DTOs.Admin;

namespace Taskpilot.API.Services;

/// <summary>
/// Reads and updates the organization's single settings row. Currently the enforced
/// storage limits (per-file cap and total quota); more org settings land here as they
/// gain enforcement.
/// </summary>
public interface IOrganizationSettingsService
{
    /// <summary>
    /// Returns the current settings plus how many bytes of storage are already in use,
    /// so the admin can see the quota against real usage.
    /// </summary>
    Task<OrganizationSettingsDto> GetAsync();

    /// <summary>
    /// Updates only the storage limits (leaves the feature flags untouched). Limits must be
    /// positive and a single file may not be allowed to exceed the whole quota.
    /// </summary>
    Task<Result<OrganizationSettingsDto>> UpdateStorageAsync(UpdateStorageDto dto, Guid adminId, string? adminEmail, string? ip);

    /// <summary>Updates only the feature flags (leaves the storage limits untouched).</summary>
    Task<Result<OrganizationSettingsDto>> UpdateFeaturesAsync(UpdateFeaturesDto dto, Guid adminId, string? adminEmail, string? ip);

    /// <summary>
    /// Updates only the general details (the organization name), leaving every other group
    /// untouched. The name must not be blank.
    /// </summary>
    Task<Result<OrganizationSettingsDto>> UpdateGeneralAsync(UpdateGeneralDto dto, Guid adminId, string? adminEmail, string? ip);

    /// <summary>
    /// Returns the public branding (organization name) for pages shown before sign-in.
    /// Readable anonymously, so it must expose nothing beyond the name.
    /// </summary>
    Task<OrganizationBrandingDto> GetBrandingAsync();

    /// <summary>
    /// Updates only the registration controls — the email-domain allowlist — leaving the
    /// storage limits and feature flags untouched. The value is normalized before storage.
    /// </summary>
    Task<Result<OrganizationSettingsDto>> UpdateRegistrationAsync(UpdateRegistrationDto dto, Guid adminId, string? adminEmail, string? ip);

    /// <summary>
    /// Returns just the feature flags — readable by any signed-in user so the client can
    /// hide navigation for disabled features.
    /// </summary>
    Task<FeatureFlagsDto> GetFeatureFlagsAsync();
}
