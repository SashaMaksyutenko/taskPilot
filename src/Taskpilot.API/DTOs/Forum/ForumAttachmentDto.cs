namespace Taskpilot.API.DTOs.Forum;

/// <summary>One file attached to a forum topic, as shown under the post.</summary>
public class ForumAttachmentDto
{
    /// <summary>Id of the attachment link (used to detach it).</summary>
    public Guid Id { get; set; }

    /// <summary>Id of the stored file (used to download it via /api/files/{id}).</summary>
    public Guid FileId { get; set; }

    /// <summary>Original file name, for display and download.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>MIME type — lets the client show a thumbnail for images.</summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>File size in bytes.</summary>
    public long SizeBytes { get; set; }

    /// <summary>Who attached the file.</summary>
    public Guid UploadedById { get; set; }

    /// <summary>Display name of whoever attached it; null if the account is gone.</summary>
    public string? UploadedByName { get; set; }

    /// <summary>UTC time the file was attached.</summary>
    public DateTime CreatedAt { get; set; }
}
