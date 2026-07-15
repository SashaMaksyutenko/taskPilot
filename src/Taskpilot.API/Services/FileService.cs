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
    // Max upload size: 10 MB (per the product spec).
    private const long MaxFileSizeBytes = 10 * 1024 * 1024;

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
    public async Task<Result<FileAttachmentDto>> SaveAsync(IFormFile file, Guid uploaderId)
    {
        if (file is null || file.Length == 0)
            return Result<FileAttachmentDto>.Fail("No file was provided.");

        if (file.Length > MaxFileSizeBytes)
            return Result<FileAttachmentDto>.Fail("File exceeds the 10 MB limit.");

        _logger.LogInformation("Saving file. Name: {Name}, Size: {Size}, UploaderId: {UploaderId}",
            file.FileName, file.Length, uploaderId);

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
            };
            _context.FileAttachments.Add(entity);
            await _context.SaveChangesAsync();

            _logger.LogInformation("File saved. FileId: {FileId}", entity.Id);
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

    /// <summary>Generates an unguessable URL-safe share token.</summary>
    private static string GenerateToken() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
}
