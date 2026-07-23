namespace Taskpilot.API.Models;

/// <summary>
/// Metadata for an uploaded file. The bytes are stored on disk under
/// <see cref="StoredName"/>; this row keeps the original name, type and size.
/// </summary>
public class FileAttachment
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Original file name as provided by the uploader (for display/download).</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Unique name the file is saved under on disk (avoids collisions/path tricks).</summary>
    public string StoredName { get; set; } = string.Empty;

    /// <summary>MIME content type (e.g. "image/png").</summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>File size in bytes.</summary>
    public long SizeBytes { get; set; }

    /// <summary>User who uploaded the file (foreign key).</summary>
    public Guid UploaderId { get; set; }

    /// <summary>Navigation to the uploader.</summary>
    public User Uploader { get; set; } = null!;

    /// <summary>UTC time the file was uploaded.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // --- Version history ---

    /// <summary>
    /// Version number, starting at 1. Re-uploading a task attachment stores the new bytes
    /// as a fresh row with the next number and repoints the attachment at it; the old row
    /// is kept as history.
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// The version this one replaced, or null for the first version. Forms a backward chain
    /// (newest → oldest) that <c>GetVersionsAsync</c> walks to list a file's history.
    /// </summary>
    public Guid? PreviousVersionId { get; set; }

    /// <summary>Navigation to the previous version, if any.</summary>
    public FileAttachment? PreviousVersion { get; set; }

    // --- Public share link (opt-in) ---

    /// <summary>
    /// Secret token for the file's public share link; null while the file is not shared.
    /// Anyone holding the token can download the file without signing in.
    /// </summary>
    public string? ShareToken { get; set; }

    /// <summary>UTC time the share link was created; null while not shared.</summary>
    public DateTime? SharedAt { get; set; }
}
