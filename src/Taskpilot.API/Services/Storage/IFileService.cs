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

    /// <summary>
    /// Saves an uploaded file as the next version of an existing one: same size/quota rules,
    /// but the new row carries the next version number and points back at <paramref name="previousFileId"/>.
    /// The caller repoints its attachment at the returned file; the old version is kept as history.
    /// </summary>
    Task<Result<FileAttachmentDto>> SaveVersionAsync(IFormFile file, Guid uploaderId, Guid previousFileId);

    /// <summary>Lists a file's version history (newest first), walking back from the current version.</summary>
    Task<Result<List<FileVersionDto>>> GetVersionsAsync(Guid currentFileId);

    /// <summary>
    /// Permanently removes a file and every earlier version it supersedes — rows and bytes —
    /// so nothing is left behind in storage. Only the uploader may do this. Used when an
    /// attachment is detached or its owning task is deleted.
    /// </summary>
    Task<Result> DeleteWithVersionsAsync(Guid currentFileId, Guid userId);

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

    /// <summary>
    /// Permanently removes a file — both its metadata row and its bytes. Only the
    /// uploader may delete their file.
    /// </summary>
    /// <remarks>
    /// Refuses while a chat message still points at the file: that foreign key is
    /// Restrict on purpose (a message keeps its attachment even if the message goes),
    /// so deleting would either fail at the database or silently gut someone's message.
    /// </remarks>
    /// <returns>Ok when the file is gone; a failure describing why not otherwise.</returns>
    Task<Result> DeleteAsync(Guid fileId, Guid userId);
}
