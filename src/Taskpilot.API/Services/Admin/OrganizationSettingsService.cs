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
        var usedBytes = await _context.FileAttachments.SumAsync(f => (long?)f.SizeBytes) ?? 0;

        _logger.LogInformation("Organization settings read. UsedBytes: {Used}, Quota: {Quota}",
            usedBytes, settings.StorageQuotaBytes);

        return new OrganizationSettingsDto
        {
            MaxUploadBytes = settings.MaxUploadBytes,
            StorageQuotaBytes = settings.StorageQuotaBytes,
            StorageUsedBytes = usedBytes,
            MarketplaceEnabled = settings.MarketplaceEnabled,
            ForumEnabled = settings.ForumEnabled,
            UpdatedAt = settings.UpdatedAt,
        };
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
    public async Task<Result<OrganizationSettingsDto>> UpdateAsync(
        UpdateOrganizationSettingsDto dto, Guid adminId, string? adminEmail, string? ip)
    {
        _logger.LogInformation("UpdateOrganizationSettings by {AdminId}. MaxUpload: {Max}, Quota: {Quota}",
            adminId, dto.MaxUploadBytes, dto.StorageQuotaBytes);

        // Both limits must be positive; a zero or negative limit would silently block
        // every upload with a confusing error.
        if (dto.MaxUploadBytes < 1 || dto.StorageQuotaBytes < 1)
            return Result<OrganizationSettingsDto>.Fail("Limits must be greater than zero.");

        // A single file can never be allowed to exceed the whole organization's quota.
        if (dto.MaxUploadBytes > dto.StorageQuotaBytes)
            return Result<OrganizationSettingsDto>.Fail("The per-file limit cannot exceed the storage quota.");

        // The row is seeded by the migration, but create it defensively if it is somehow
        // missing (e.g. a database restored from before the seed).
        var settings = await _context.OrganizationSettings.FirstOrDefaultAsync();
        if (settings is null)
        {
            settings = new OrganizationSettings { Id = OrganizationSettings.SingletonId };
            _context.OrganizationSettings.Add(settings);
        }

        settings.MaxUploadBytes = dto.MaxUploadBytes;
        settings.StorageQuotaBytes = dto.StorageQuotaBytes;
        settings.MarketplaceEnabled = dto.MarketplaceEnabled;
        settings.ForumEnabled = dto.ForumEnabled;
        settings.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        await _audit.LogAsync(
            action: "admin.settings.updated",
            actorId: adminId,
            actorEmail: adminEmail,
            entityType: nameof(OrganizationSettings),
            entityId: settings.Id.ToString(),
            details: $"MaxUpload={dto.MaxUploadBytes}; Quota={dto.StorageQuotaBytes}; " +
                     $"Marketplace={dto.MarketplaceEnabled}; Forum={dto.ForumEnabled}",
            ipAddress: ip);

        _logger.LogInformation("Organization settings updated by {AdminId}.", adminId);

        var usedBytes = await _context.FileAttachments.SumAsync(f => (long?)f.SizeBytes) ?? 0;
        return Result<OrganizationSettingsDto>.Ok(new OrganizationSettingsDto
        {
            MaxUploadBytes = settings.MaxUploadBytes,
            StorageQuotaBytes = settings.StorageQuotaBytes,
            StorageUsedBytes = usedBytes,
            MarketplaceEnabled = settings.MarketplaceEnabled,
            ForumEnabled = settings.ForumEnabled,
            UpdatedAt = settings.UpdatedAt,
        });
    }

    /// <summary>
    /// Reads the singleton settings row, or a defaults-only instance when the row is
    /// missing — so reads never fail on a database that predates the seed.
    /// </summary>
    private async Task<OrganizationSettings> LoadOrDefaultAsync() =>
        await _context.OrganizationSettings.AsNoTracking().FirstOrDefaultAsync()
        ?? new OrganizationSettings();
}
