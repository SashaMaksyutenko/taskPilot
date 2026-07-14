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

    // --- Public share link (opt-in) ---

    /// <summary>
    /// Secret token for the file's public share link; null while the file is not shared.
    /// Anyone holding the token can download the file without signing in.
    /// </summary>
    public string? ShareToken { get; set; }

    /// <summary>UTC time the share link was created; null while not shared.</summary>
    public DateTime? SharedAt { get; set; }
}
