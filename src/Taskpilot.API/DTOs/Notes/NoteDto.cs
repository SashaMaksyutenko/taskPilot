namespace Taskpilot.API.DTOs.Notes;

/// <summary>A note as returned to clients.</summary>
public class NoteDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? Color { get; set; }
    public bool IsPinned { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<string> Tags { get; set; } = new();
}
