namespace Taskpilot.API.DTOs.Files;

/// <summary>One entry in a file's version history.</summary>
public class FileVersionDto
{
    /// <summary>Id of this version's stored file (download it via /api/files/{id}).</summary>
    public Guid FileId { get; set; }

    /// <summary>Version number, 1 being the first upload.</summary>
    public int Version { get; set; }

    /// <summary>File name this version was uploaded under.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Size of this version in bytes.</summary>
    public long SizeBytes { get; set; }

    /// <summary>Who uploaded this version; null if the account is gone.</summary>
    public string? UploadedByName { get; set; }

    /// <summary>UTC time this version was uploaded.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>True for the version the attachment currently points at.</summary>
    public bool IsCurrent { get; set; }
}
