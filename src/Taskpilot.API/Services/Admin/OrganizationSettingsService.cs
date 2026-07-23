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
    private readonly IFileService _files;
    private readonly ILogger<OrganizationSettingsService> _logger;

    /// <summary>Largest logo image accepted: 2 MB (a logo needs no more).</summary>
    private const long MaxLogoBytes = 2L * 1024 * 1024;

    public OrganizationSettingsService(
        TaskpilotDbContext context,
        IAuditService audit,
        IFileService files,
        ILogger<OrganizationSettingsService> logger)
    {
        _context = context;
        _audit = audit;
        _files = files;
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
    public async Task<OrganizationBrandingDto> GetBrandingAsync()
    {
        var settings = await LoadOrDefaultAsync();
        return new OrganizationBrandingDto { Name = settings.Name, LogoUrl = LogoUrl(settings.LogoFileId) };
    }

    /// <inheritdoc />
    public async Task<Result<OrganizationSettingsDto>> UpdateLogoAsync(
        IFormFile file, Guid adminId, string? adminEmail, string? ip)
    {
        _logger.LogInformation("UpdateLogo by {AdminId}. Size: {Size}", adminId, file?.Length ?? 0);

        if (file is null || file.Length == 0)
            return Result<OrganizationSettingsDto>.Fail("No file was provided.");
        if (file.Length > MaxLogoBytes)
            return Result<OrganizationSettingsDto>.Fail("Logo exceeds the 2 MB limit.");
        // Only images make sense as a logo.
        if (string.IsNullOrWhiteSpace(file.ContentType) ||
            !file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return Result<OrganizationSettingsDto>.Fail("Logo must be an image.");

        // Persist the image bytes + metadata through the shared file storage.
        var saved = await _files.SaveAsync(file, adminId);
        if (!saved.Succeeded)
            return Result<OrganizationSettingsDto>.Fail(saved.Error!);

        var settings = await GetOrCreateAsync();
        // Remember the outgoing logo: nothing else references it, so once the pointer moves
        // it would be orphaned (row + bytes) forever — the same trap as replaced avatars.
        var previousLogoId = settings.LogoFileId;
        settings.LogoFileId = saved.Value!.Id;
        settings.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        await DeleteReplacedLogoAsync(previousLogoId);

        await _audit.LogAsync(
            action: "admin.settings.logo.updated",
            actorId: adminId,
            actorEmail: adminEmail,
            entityType: nameof(OrganizationSettings),
            entityId: settings.Id.ToString(),
            details: $"LogoFileId={saved.Value.Id}",
            ipAddress: ip);

        _logger.LogInformation("Logo updated by {AdminId}. FileId: {FileId}", adminId, saved.Value.Id);
        return Result<OrganizationSettingsDto>.Ok(await BuildDtoAsync(settings));
    }

    /// <inheritdoc />
    public async Task<Result<OrganizationSettingsDto>> RemoveLogoAsync(Guid adminId, string? adminEmail, string? ip)
    {
        var settings = await GetOrCreateAsync();
        var previousLogoId = settings.LogoFileId;
        if (previousLogoId is null)
            // Nothing to remove; report the current state rather than an error.
            return Result<OrganizationSettingsDto>.Ok(await BuildDtoAsync(settings));

        settings.LogoFileId = null;
        settings.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        await DeleteReplacedLogoAsync(previousLogoId);

        await _audit.LogAsync(
            action: "admin.settings.logo.removed",
            actorId: adminId,
            actorEmail: adminEmail,
            entityType: nameof(OrganizationSettings),
            entityId: settings.Id.ToString(),
            details: "Logo cleared",
            ipAddress: ip);

        _logger.LogInformation("Logo removed by {AdminId}.", adminId);
        return Result<OrganizationSettingsDto>.Ok(await BuildDtoAsync(settings));
    }

    /// <inheritdoc />
    public async Task<Result<FileDownload>> GetLogoAsync()
    {
        var logoId = await _context.OrganizationSettings.AsNoTracking()
            .Select(s => s.LogoFileId).FirstOrDefaultAsync();
        if (logoId is null)
            return Result<FileDownload>.Fail("No logo is set.");

        return await _files.GetForDownloadAsync(logoId.Value);
    }

    /// <inheritdoc />
    public async Task<Result<OrganizationSettingsDto>> UpdateGeneralAsync(
        UpdateGeneralDto dto, Guid adminId, string? adminEmail, string? ip)
    {
        var name = dto.Name?.Trim() ?? string.Empty;
        _logger.LogInformation("UpdateGeneral by {AdminId}. Name: {Name}", adminId, name);

        // A blank name would erase the brand everywhere it is shown; refuse it.
        if (name.Length == 0)
            return Result<OrganizationSettingsDto>.Fail("Organization name is required.");
        // Matches the column length so the update fails cleanly rather than at the database.
        if (name.Length > 100)
            return Result<OrganizationSettingsDto>.Fail("Organization name is too long (max 100 characters).");

        var settings = await GetOrCreateAsync();
        // Touches ONLY the name — storage, features and registration are left as they were.
        settings.Name = name;
        settings.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        await _audit.LogAsync(
            action: "admin.settings.general.updated",
            actorId: adminId,
            actorEmail: adminEmail,
            entityType: nameof(OrganizationSettings),
            entityId: settings.Id.ToString(),
            details: $"Name={name}",
            ipAddress: ip);

        _logger.LogInformation("General settings updated by {AdminId}.", adminId);
        return Result<OrganizationSettingsDto>.Ok(await BuildDtoAsync(settings));
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
        _logger.LogInformation("UpdateRegistration by {AdminId}. Allowed: {Allowed}, Blocked: {Blocked}",
            adminId, dto.AllowedEmailDomains, dto.BlockedEmailDomains);

        // Normalize through the parser and store the canonical form, so the stored value is
        // always clean ("@Acme.COM, , foo" -> "acme.com, foo").
        var allowed = string.Join(", ", EmailDomainList.Parse(dto.AllowedEmailDomains).Domains);
        var blocked = string.Join(", ", EmailDomainList.Parse(dto.BlockedEmailDomains).Domains);

        // A negative limit is meaningless; treat it as "unlimited" rather than rejecting.
        var maxMembers = Math.Max(0, dto.MaxMembers);

        var settings = await GetOrCreateAsync();
        // Touches ONLY the registration fields — storage and features are left as they were.
        settings.AllowedEmailDomains = allowed;
        settings.BlockedEmailDomains = blocked;
        settings.MaxMembers = maxMembers;
        settings.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        await _audit.LogAsync(
            action: "admin.settings.registration.updated",
            actorId: adminId,
            actorEmail: adminEmail,
            entityType: nameof(OrganizationSettings),
            entityId: settings.Id.ToString(),
            details: $"Allowed={(allowed.Length == 0 ? "(any)" : allowed)}; " +
                     $"Blocked={(blocked.Length == 0 ? "(none)" : blocked)}; " +
                     $"MaxMembers={(maxMembers == 0 ? "(unlimited)" : maxMembers.ToString())}",
            ipAddress: ip);

        _logger.LogInformation("Registration settings updated by {AdminId}.", adminId);
        return Result<OrganizationSettingsDto>.Ok(await BuildDtoAsync(settings));
    }

    /// <summary>Builds the read DTO for a settings row, including live storage usage.</summary>
    private async Task<OrganizationSettingsDto> BuildDtoAsync(OrganizationSettings settings)
    {
        var usedBytes = await _context.FileAttachments.SumAsync(f => (long?)f.SizeBytes) ?? 0;
        var activeMembers = await _context.Users.CountAsync(u => u.IsActive);
        return new OrganizationSettingsDto
        {
            Name = settings.Name,
            LogoUrl = LogoUrl(settings.LogoFileId),
            MaxUploadBytes = settings.MaxUploadBytes,
            StorageQuotaBytes = settings.StorageQuotaBytes,
            StorageUsedBytes = usedBytes,
            MarketplaceEnabled = settings.MarketplaceEnabled,
            ForumEnabled = settings.ForumEnabled,
            AllowedEmailDomains = settings.AllowedEmailDomains,
            BlockedEmailDomains = settings.BlockedEmailDomains,
            MaxMembers = settings.MaxMembers,
            ActiveMembers = activeMembers,
            UpdatedAt = settings.UpdatedAt,
        };
    }

    /// <summary>
    /// Builds the public URL for the current logo, or null when none is set. The logo id is
    /// used as a cache-busting token, so replacing the logo (new id) makes browsers refetch
    /// even though the endpoint path is constant.
    /// </summary>
    private static string? LogoUrl(Guid? logoFileId) =>
        logoFileId is { } id ? $"/api/settings/logo?v={id}" : null;

    /// <summary>
    /// Deletes a logo image the organization no longer points at, so replacing or removing
    /// the logo does not leak a file row and its bytes. Best-effort: the settings change is
    /// already saved, so a cleanup failure must not fail the request. Deletes as the image's
    /// own uploader, since a later admin may be replacing a logo an earlier admin uploaded.
    /// </summary>
    private async Task DeleteReplacedLogoAsync(Guid? previousLogoId)
    {
        if (previousLogoId is not { } fileId)
            return;

        var uploaderId = await _context.FileAttachments.AsNoTracking()
            .Where(f => f.Id == fileId).Select(f => f.UploaderId).FirstOrDefaultAsync();

        var deleted = await _files.DeleteAsync(fileId, uploaderId);
        if (!deleted.Succeeded)
            _logger.LogWarning("Could not delete the replaced logo. FileId: {FileId}, Reason: {Reason}",
                fileId, deleted.Error);
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
