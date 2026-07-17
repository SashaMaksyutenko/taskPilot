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
    /// Updates the storage limits. Both must be positive and a single file may not be
    /// allowed to exceed the whole quota.
    /// </summary>
    Task<Result<OrganizationSettingsDto>> UpdateAsync(UpdateOrganizationSettingsDto dto, Guid adminId, string? adminEmail, string? ip);
}
