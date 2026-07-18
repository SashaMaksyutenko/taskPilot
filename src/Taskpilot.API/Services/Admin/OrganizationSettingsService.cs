using Microsoft.EntityFrameworkCore;
using Taskpilot.API.Common;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Admin;
using Taskpilot.API.Models;

namespace Taskpilot.API.Services;

/// <summary>
/// Reads and updates the organization's single settings row (see
/// <see cref="OrganizationSettings"/>). Persists admin changes and records them in the
/// audit trail.
/// </summary>
public class OrganizationSettingsService : IOrganizationSettingsService
{
    private readonly TaskpilotDbContext _context;
    private readonly IAuditService _audit;
    private readonly ILogger<OrganizationSettingsService> _logger;

    public OrganizationSettingsService(
        TaskpilotDbContext context,
        IAuditService audit,
        ILogger<OrganizationSettingsService> logger)
    {
        _context = context;
        _audit = audit;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<OrganizationSettingsDto> GetAsync()
    {
        var settings = await LoadOrDefaultAsync();
        _logger.LogInformation("Organization settings read. Quota: {Quota}", settings.StorageQuotaBytes);
        return await BuildDtoAsync(settings);
    }

    /// <inheritdoc />
    public async Task<FeatureFlagsDto> GetFeatureFlagsAsync()
    {
        var settings = await LoadOrDefaultAsync();
        return new FeatureFlagsDto
        {
            MarketplaceEnabled = settings.MarketplaceEnabled,
            ForumEnabled = settings.ForumEnabled,
        };
    }

    /// <inheritdoc />
    public async Task<Result<OrganizationSettingsDto>> UpdateStorageAsync(
        UpdateStorageDto dto, Guid adminId, string? adminEmail, string? ip)
    {
        _logger.LogInformation("UpdateStorage by {AdminId}. MaxUpload: {Max}, Quota: {Quota}",
            adminId, dto.MaxUploadBytes, dto.StorageQuotaBytes);

        // Both limits must be positive; a zero or negative limit would silently block
        // every upload with a confusing error.
        if (dto.MaxUploadBytes < 1 || dto.StorageQuotaBytes < 1)
            return Result<OrganizationSettingsDto>.Fail("Limits must be greater than zero.");

        // A single file can never be allowed to exceed the whole organization's quota.
        if (dto.MaxUploadBytes > dto.StorageQuotaBytes)
            return Result<OrganizationSettingsDto>.Fail("The per-file limit cannot exceed the storage quota.");

        var settings = await GetOrCreateAsync();
        // Touches ONLY the storage fields — the feature flags are left exactly as they were.
        settings.MaxUploadBytes = dto.MaxUploadBytes;
        settings.StorageQuotaBytes = dto.StorageQuotaBytes;
        settings.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        await _audit.LogAsync(
            action: "admin.settings.storage.updated",
            actorId: adminId,
            actorEmail: adminEmail,
            entityType: nameof(OrganizationSettings),
            entityId: settings.Id.ToString(),
            details: $"MaxUpload={dto.MaxUploadBytes}; Quota={dto.StorageQuotaBytes}",
            ipAddress: ip);

        _logger.LogInformation("Storage limits updated by {AdminId}.", adminId);
        return Result<OrganizationSettingsDto>.Ok(await BuildDtoAsync(settings));
    }

    /// <inheritdoc />
    public async Task<Result<OrganizationSettingsDto>> UpdateFeaturesAsync(
        UpdateFeaturesDto dto, Guid adminId, string? adminEmail, string? ip)
    {
        _logger.LogInformation("UpdateFeatures by {AdminId}. Marketplace: {M}, Forum: {F}",
            adminId, dto.MarketplaceEnabled, dto.ForumEnabled);

        var settings = await GetOrCreateAsync();
        // Touches ONLY the feature flags — the storage limits are left exactly as they were.
        settings.MarketplaceEnabled = dto.MarketplaceEnabled;
        settings.ForumEnabled = dto.ForumEnabled;
        settings.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        await _audit.LogAsync(
            action: "admin.settings.features.updated",
            actorId: adminId,
            actorEmail: adminEmail,
            entityType: nameof(OrganizationSettings),
            entityId: settings.Id.ToString(),
            details: $"Marketplace={dto.MarketplaceEnabled}; Forum={dto.ForumEnabled}",
            ipAddress: ip);

        _logger.LogInformation("Feature flags updated by {AdminId}.", adminId);
        return Result<OrganizationSettingsDto>.Ok(await BuildDtoAsync(settings));
    }

    /// <inheritdoc />
    public async Task<Result<OrganizationSettingsDto>> UpdateRegistrationAsync(
        UpdateRegistrationDto dto, Guid adminId, string? adminEmail, string? ip)
    {
        _logger.LogInformation("UpdateRegistration by {AdminId}. Domains: {Domains}", adminId, dto.AllowedEmailDomains);

        // Normalize through the allowlist parser and store its canonical form, so the stored
        // value is always clean ("@Acme.COM, , foo" -> "acme.com, foo").
        var normalized = string.Join(", ", EmailDomainAllowlist.Parse(dto.AllowedEmailDomains).Domains);

        var settings = await GetOrCreateAsync();
        // Touches ONLY the registration fields — storage and features are left as they were.
        settings.AllowedEmailDomains = normalized;
        settings.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        await _audit.LogAsync(
            action: "admin.settings.registration.updated",
            actorId: adminId,
            actorEmail: adminEmail,
            entityType: nameof(OrganizationSettings),
            entityId: settings.Id.ToString(),
            details: $"AllowedEmailDomains={(normalized.Length == 0 ? "(any)" : normalized)}",
            ipAddress: ip);

        _logger.LogInformation("Registration settings updated by {AdminId}.", adminId);
        return Result<OrganizationSettingsDto>.Ok(await BuildDtoAsync(settings));
    }

    /// <summary>Builds the read DTO for a settings row, including live storage usage.</summary>
    private async Task<OrganizationSettingsDto> BuildDtoAsync(OrganizationSettings settings)
    {
        var usedBytes = await _context.FileAttachments.SumAsync(f => (long?)f.SizeBytes) ?? 0;
        return new OrganizationSettingsDto
        {
            MaxUploadBytes = settings.MaxUploadBytes,
            StorageQuotaBytes = settings.StorageQuotaBytes,
            StorageUsedBytes = usedBytes,
            MarketplaceEnabled = settings.MarketplaceEnabled,
            ForumEnabled = settings.ForumEnabled,
            AllowedEmailDomains = settings.AllowedEmailDomains,
            UpdatedAt = settings.UpdatedAt,
        };
    }

    /// <summary>
    /// Returns the tracked singleton settings row, creating it if it is somehow missing
    /// (e.g. a database restored from before the settings seed).
    /// </summary>
    private async Task<OrganizationSettings> GetOrCreateAsync()
    {
        var settings = await _context.OrganizationSettings.FirstOrDefaultAsync();
        if (settings is null)
        {
            settings = new OrganizationSettings { Id = OrganizationSettings.SingletonId };
            _context.OrganizationSettings.Add(settings);
        }
        return settings;
    }

    /// <summary>
    /// Reads the singleton settings row, or a defaults-only instance when the row is
    /// missing — so reads never fail on a database that predates the seed.
    /// </summary>
    private async Task<OrganizationSettings> LoadOrDefaultAsync() =>
        await _context.OrganizationSettings.AsNoTracking().FirstOrDefaultAsync()
        ?? new OrganizationSettings();
}
