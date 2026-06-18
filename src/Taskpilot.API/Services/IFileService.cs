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
}
