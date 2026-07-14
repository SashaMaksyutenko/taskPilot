using Taskpilot.API.Common;
using Taskpilot.API.DTOs.Files;

namespace Taskpilot.API.Services;

/// <summary>
/// Data needed to stream a stored file back to the client.
/// </summary>
/// <param name="PhysicalPath">Absolute path of the file on disk.</param>
/// <param name="ContentType">MIME type to send.</param>
/// <param name="FileName">Original file name for the download.</param>
public record FileDownload(string PhysicalPath, string ContentType, string FileName);

/// <summary>
/// Handles file uploads and downloads (storage + metadata).
/// </summary>
public interface IFileService
{
    /// <summary>Saves an uploaded file to disk and records its metadata.</summary>
    Task<Result<FileAttachmentDto>> SaveAsync(IFormFile file, Guid uploaderId);

    /// <summary>Resolves a stored file by id for download.</summary>
    Task<Result<FileDownload>> GetForDownloadAsync(Guid id);

    /// <summary>
    /// Creates (or returns the existing) public share token for a file. Only the
    /// uploader may share it. Anyone holding the token can download without signing in.
    /// </summary>
    Task<Result<string>> CreateShareTokenAsync(Guid fileId, Guid userId);

    /// <summary>Revokes a file's share link (uploader only), invalidating the token.</summary>
    Task<Result> RevokeShareAsync(Guid fileId, Guid userId);

    /// <summary>Resolves a shared file by its token for anonymous download.</summary>
    Task<Result<FileDownload>> GetForDownloadByTokenAsync(string token);
}
