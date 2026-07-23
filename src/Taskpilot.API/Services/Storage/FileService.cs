using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Taskpilot.API.Common;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Files;
using Taskpilot.API.Models;

namespace Taskpilot.API.Services;

/// <summary>
/// Stores uploaded files on the local disk (under "uploads" in the content root)
/// and keeps their metadata in the database. Cloud storage can replace the disk
/// part later without touching callers.
/// </summary>
public class FileService : IFileService
{
    private readonly TaskpilotDbContext _context;
    private readonly IFileStorage _storage;
    private readonly ILogger<FileService> _logger;

    public FileService(TaskpilotDbContext context, IFileStorage storage, ILogger<FileService> logger)
    {
        _context = context;
        _storage = storage;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<Result<FileAttachmentDto>> SaveAsync(IFormFile file, Guid uploaderId) =>
        StoreAsync(file, uploaderId, version: 1, previousVersionId: null);

    /// <inheritdoc />
    public async Task<Result<FileAttachmentDto>> SaveVersionAsync(IFormFile file, Guid uploaderId, Guid previousFileId)
    {
        var previous = await _context.FileAttachments.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == previousFileId);
        if (previous is null)
            return Result<FileAttachmentDto>.Fail("File not found.");

        // The new bytes become the next version and point back at the one they replace.
        return await StoreAsync(file, uploaderId, previous.Version + 1, previous.Id);
    }

    /// <summary>
    /// Shared upload path for both a first upload and a new version: enforces the size and
    /// organization-quota limits, writes the bytes and records the metadata row.
    /// </summary>
    private async Task<Result<FileAttachmentDto>> StoreAsync(
        IFormFile file, Guid uploaderId, int version, Guid? previousVersionId)
    {
        if (file is null || file.Length == 0)
            return Result<FileAttachmentDto>.Fail("No file was provided.");

        // Limits come from the admin-editable organization settings, not a constant, so an
        // admin can change them without a redeploy.
        var (maxUploadBytes, storageQuotaBytes) = await GetStorageLimitsAsync();

        if (file.Length > maxUploadBytes)
            return Result<FileAttachmentDto>.Fail($"File exceeds the {FormatSize(maxUploadBytes)} limit.");

        // Enforce the whole-organization quota: reject if this upload would push total
        // stored bytes over it. Old versions still occupy storage, so they count here too.
        // SumAsync over an empty table returns null, hence the cast.
        var usedBytes = await _context.FileAttachments.SumAsync(f => (long?)f.SizeBytes) ?? 0;
        if (usedBytes + file.Length > storageQuotaBytes)
        {
            _logger.LogWarning("Upload blocked: storage quota reached. Used: {Used}, Quota: {Quota}, Incoming: {Size}",
                usedBytes, storageQuotaBytes, file.Length);
            return Result<FileAttachmentDto>.Fail("The organization's storage quota has been reached.");
        }

        _logger.LogInformation("Saving file. Name: {Name}, Size: {Size}, Version: {Version}, UploaderId: {UploaderId}",
            file.FileName, file.Length, version, uploaderId);

        try
        {
            // Random storage key keeps the original extension but avoids collisions
            // and path-traversal from user-supplied names.
            var storedName = Guid.NewGuid().ToString("N") + Path.GetExtension(file.FileName);
            var contentType = string.IsNullOrWhiteSpace(file.ContentType)
                ? "application/octet-stream"
                : file.ContentType;

            await using (var upload = file.OpenReadStream())
            {
                await _storage.SaveAsync(storedName, upload, contentType);
            }

            var entity = new FileAttachment
            {
                Id = Guid.NewGuid(),
                FileName = Path.GetFileName(file.FileName),
                StoredName = storedName,
                ContentType = contentType,
                SizeBytes = file.Length,
                UploaderId = uploaderId,
                CreatedAt = DateTime.UtcNow,
                Version = version,
                PreviousVersionId = previousVersionId,
            };
            _context.FileAttachments.Add(entity);
            await _context.SaveChangesAsync();

            _logger.LogInformation("File saved. FileId: {FileId}, Version: {Version}", entity.Id, entity.Version);
            return Result<FileAttachmentDto>.Ok(new FileAttachmentDto
            {
                Id = entity.Id,
                FileName = entity.FileName,
                ContentType = entity.ContentType,
                SizeBytes = entity.SizeBytes,
                CreatedAt = entity.CreatedAt,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving file. UploaderId: {UploaderId}", uploaderId);
            return Result<FileAttachmentDto>.Fail("An unexpected error occurred while saving the file.");
        }
    }

    /// <inheritdoc />
    public async Task<Result<List<FileVersionDto>>> GetVersionsAsync(Guid currentFileId)
    {
        var chain = await LoadVersionChainAsync(currentFileId);
        if (chain.Count == 0)
            return Result<List<FileVersionDto>>.Fail("File not found.");

        // Resolve uploader names in one query rather than per version.
        var uploaderIds = chain.Select(f => f.UploaderId).Distinct().ToList();
        var names = await _context.Users.AsNoTracking()
            .Where(u => uploaderIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Name);

        var versions = chain.Select(f => new FileVersionDto
        {
            FileId = f.Id,
            Version = f.Version,
            FileName = f.FileName,
            SizeBytes = f.SizeBytes,
            UploadedByName = names.GetValueOrDefault(f.UploaderId),
            CreatedAt = f.CreatedAt,
            IsCurrent = f.Id == currentFileId,
        }).ToList();

        return Result<List<FileVersionDto>>.Ok(versions);
    }

    /// <inheritdoc />
    public async Task<Result> DeleteWithVersionsAsync(Guid currentFileId, Guid userId)
    {
        var chain = await LoadVersionChainAsync(currentFileId);
        if (chain.Count == 0)
            return Result.Fail("File not found.");

        // Only the uploader may delete. New versions can only be added by the attachment's
        // owner, so every version in the chain shares one uploader — checking the head is enough.
        if (chain[0].UploaderId != userId)
        {
            _logger.LogWarning("Delete blocked: not the uploader. FileId: {FileId}, UserId: {UserId}", currentFileId, userId);
            return Result.Fail("Only the uploader can delete this file.");
        }

        // Remove the rows newest → oldest so each PreviousVersion (a Restrict foreign key)
        // is already free of references by the time it is deleted.
        var storedNames = chain.Select(f => f.StoredName).ToList();
        foreach (var file in chain)
        {
            _context.FileAttachments.Remove(file);
            await _context.SaveChangesAsync();
        }

        // Bytes last, and never fatally: once the rows are gone the bytes are just garbage.
        foreach (var storedName in storedNames)
        {
            try
            {
                await _storage.DeleteAsync(storedName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "File row deleted but its bytes could not be removed. StoredName: {StoredName}", storedName);
            }
        }

        _logger.LogInformation("File and {Count} version(s) deleted. FileId: {FileId}, By: {UserId}",
            chain.Count, currentFileId, userId);
        return Result.Ok();
    }

    /// <summary>
    /// Loads a file and every earlier version it supersedes, newest first. Empty if the
    /// starting file does not exist. A guard bounds the walk in case data is ever cyclic.
    /// </summary>
    private async Task<List<FileAttachment>> LoadVersionChainAsync(Guid currentFileId)
    {
        var chain = new List<FileAttachment>();
        var current = await _context.FileAttachments.FirstOrDefaultAsync(f => f.Id == currentFileId);
        var guard = 0;
        while (current is not null && guard++ < 1000)
        {
            chain.Add(current);
            current = current.PreviousVersionId is { } prevId
                ? await _context.FileAttachments.FirstOrDefaultAsync(f => f.Id == prevId)
                : null;
        }
        return chain;
    }

    /// <inheritdoc />
    public async Task<Result<FileDownload>> GetForDownloadAsync(Guid id)
    {
        var file = await _context.FileAttachments.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id);
        if (file is null)
            return Result<FileDownload>.Fail("File not found.");

        var content = await _storage.OpenReadAsync(file.StoredName);
        if (content is null)
            return Result<FileDownload>.Fail("File not found.");

        return Result<FileDownload>.Ok(new FileDownload(content, file.ContentType, file.FileName));
    }

    /// <inheritdoc />
    public async Task<Result<string>> CreateShareTokenAsync(Guid fileId, Guid userId)
    {
        var file = await _context.FileAttachments.FirstOrDefaultAsync(f => f.Id == fileId);
        if (file is null)
            return Result<string>.Fail("File not found.");

        // Only the person who uploaded the file may expose it publicly.
        if (file.UploaderId != userId)
            return Result<string>.Fail("Only the uploader can share this file.");

        // Sharing again returns the same link rather than rotating it.
        if (string.IsNullOrEmpty(file.ShareToken))
        {
            file.ShareToken = GenerateToken();
            file.SharedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            _logger.LogInformation("File share link created. FileId: {FileId}, By: {UserId}", fileId, userId);
        }

        return Result<string>.Ok(file.ShareToken!);
    }

    /// <inheritdoc />
    public async Task<Result> RevokeShareAsync(Guid fileId, Guid userId)
    {
        var file = await _context.FileAttachments.FirstOrDefaultAsync(f => f.Id == fileId);
        if (file is null)
            return Result.Fail("File not found.");

        if (file.UploaderId != userId)
            return Result.Fail("Only the uploader can revoke this share link.");

        file.ShareToken = null;
        file.SharedAt = null;
        await _context.SaveChangesAsync();

        _logger.LogInformation("File share link revoked. FileId: {FileId}, By: {UserId}", fileId, userId);
        return Result.Ok();
    }

    /// <inheritdoc />
    public async Task<Result<FileDownload>> GetForDownloadByTokenAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return Result<FileDownload>.Fail("File not found.");

        var file = await _context.FileAttachments
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.ShareToken == token);
        if (file is null)
            return Result<FileDownload>.Fail("File not found.");

        var content = await _storage.OpenReadAsync(file.StoredName);
        if (content is null)
            return Result<FileDownload>.Fail("File not found.");

        return Result<FileDownload>.Ok(new FileDownload(content, file.ContentType, file.FileName));
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(Guid fileId, Guid userId)
    {
        _logger.LogInformation("DeleteAsync started. FileId: {FileId}, UserId: {UserId}", fileId, userId);

        var file = await _context.FileAttachments.FirstOrDefaultAsync(f => f.Id == fileId);
        if (file is null)
            return Result.Fail("File not found.");

        if (file.UploaderId != userId)
        {
            _logger.LogWarning("Delete blocked: not the uploader. FileId: {FileId}, UserId: {UserId}", fileId, userId);
            return Result.Fail("Only the uploader can delete this file.");
        }

        // Message.FileAttachmentId is a Restrict foreign key, so the database would
        // refuse this delete anyway — catch it here and say why instead of throwing.
        if (await _context.Messages.AnyAsync(m => m.FileAttachmentId == fileId))
        {
            _logger.LogWarning("Delete blocked: file is attached to a message. FileId: {FileId}", fileId);
            return Result.Fail("This file is attached to a chat message and cannot be deleted.");
        }

        // User.AvatarFileId is a bare Guid with no foreign key, so nothing stops the
        // row from going: clear the pointer by hand or the profile renders a broken image.
        var avatarOwners = await _context.Users.Where(u => u.AvatarFileId == fileId).ToListAsync();
        foreach (var owner in avatarOwners)
        {
            owner.AvatarFileId = null;
            owner.UpdatedAt = DateTime.UtcNow;
        }

        var storedName = file.StoredName;
        _context.FileAttachments.Remove(file);
        await _context.SaveChangesAsync();

        // Bytes last, and never fatally: once the row is committed the bytes are just
        // garbage, whereas deleting them first could leave a row pointing at nothing.
        try
        {
            await _storage.DeleteAsync(storedName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "File row deleted but its bytes could not be removed. StoredName: {StoredName}", storedName);
        }

        _logger.LogInformation("File deleted. FileId: {FileId}, By: {UserId}", fileId, userId);
        return Result.Ok();
    }

    /// <summary>Generates an unguessable URL-safe share token.</summary>
    private static string GenerateToken() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();

    /// <summary>
    /// Reads the current per-file and organization storage limits from the settings row,
    /// falling back to the model defaults when the row is missing (e.g. a test database or
    /// one restored from before the settings seed).
    /// </summary>
    private async Task<(long MaxUploadBytes, long StorageQuotaBytes)> GetStorageLimitsAsync()
    {
        var settings = await _context.OrganizationSettings.AsNoTracking().FirstOrDefaultAsync();
        return (
            settings?.MaxUploadBytes ?? OrganizationSettings.DefaultMaxUploadBytes,
            settings?.StorageQuotaBytes ?? OrganizationSettings.DefaultStorageQuotaBytes);
    }

    /// <summary>
    /// Formats a byte count for a user-facing message: megabytes once it is at least 1 MB,
    /// otherwise the raw bytes — so a small (e.g. test) limit never renders as "0 MB".
    /// </summary>
    private static string FormatSize(long bytes) =>
        bytes >= 1024 * 1024 ? $"{bytes / (1024 * 1024)} MB" : $"{bytes} bytes";
}
