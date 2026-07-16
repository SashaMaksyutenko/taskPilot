using Taskpilot.API.Common;
using Taskpilot.API.DTOs.Files;

namespace Taskpilot.API.Services;

/// <summary>
/// Data needed to stream a stored file back to the client. Carries the bytes as a
/// stream rather than a disk path, so the file can just as well live in an S3 bucket.
/// The caller owns the stream and must dispose it (ASP.NET's <c>File(...)</c> does).
/// </summary>
/// <param name="Content">The file's bytes.</param>
/// <param name="ContentType">MIME type to send.</param>
/// <param name="FileName">Original file name for the download.</param>
public record FileDownload(Stream Content, string ContentType, string FileName);

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
