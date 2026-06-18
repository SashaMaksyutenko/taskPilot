namespace Taskpilot.API.DTOs.Files;

/// <summary>
/// File metadata returned to clients after upload / in listings.
/// </summary>
public class FileAttachmentDto
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTime CreatedAt { get; set; }
}
