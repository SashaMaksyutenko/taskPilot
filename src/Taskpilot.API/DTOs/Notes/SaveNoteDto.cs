namespace Taskpilot.API.DTOs.Notes;

/// <summary>Input for creating or updating a note.</summary>
public class SaveNoteDto
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? Color { get; set; }
    public bool IsPinned { get; set; }
}
