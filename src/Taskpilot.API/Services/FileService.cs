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
    private readonly ILogger<FileService> _logger;
    private readonly string _uploadsRoot;

    public FileService(TaskpilotDbContext context, IWebHostEnvironment env, ILogger<FileService> logger)
    {
        _context = context;
        _logger = logger;
        // Files live in <contentRoot>/uploads (folder is gitignored).
        _uploadsRoot = Path.Combine(env.ContentRootPath, "uploads");
        Directory.CreateDirectory(_uploadsRoot);
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
            // Random on-disk name keeps the original extension but avoids collisions
            // and path-traversal from user-supplied names.
            var storedName = Guid.NewGuid().ToString("N") + Path.GetExtension(file.FileName);
            var fullPath = Path.Combine(_uploadsRoot, storedName);

            await using (var stream = File.Create(fullPath))
            {
                await file.CopyToAsync(stream);
            }

            var entity = new FileAttachment
            {
                Id = Guid.NewGuid(),
                FileName = Path.GetFileName(file.FileName),
                StoredName = storedName,
                ContentType = string.IsNullOrWhiteSpace(file.ContentType)
                    ? "application/octet-stream"
                    : file.ContentType,
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

        var fullPath = Path.Combine(_uploadsRoot, file.StoredName);
        if (!File.Exists(fullPath))
        {
            _logger.LogWarning("File metadata exists but bytes are missing. FileId: {FileId}", id);
            return Result<FileDownload>.Fail("File not found.");
        }

        return Result<FileDownload>.Ok(new FileDownload(fullPath, file.ContentType, file.FileName));
    }
}
