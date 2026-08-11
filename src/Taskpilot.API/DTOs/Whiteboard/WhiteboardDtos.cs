namespace Taskpilot.API.DTOs.Whiteboard;

/// <summary>A sticky note as sent to clients.</summary>
public class WhiteboardNoteDto
{
    public Guid Id { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public string Text { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public Guid AuthorId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public Guid? EditedById { get; set; }
    public string? EditedByName { get; set; }
}

/// <summary>Payload to create a note.</summary>
public class CreateNoteDto
{
    public double X { get; set; }
    public double Y { get; set; }
    public string? Text { get; set; }
    public string? Color { get; set; }
}

/// <summary>Partial update — only the provided fields change (move, recolor, or edit text).</summary>
public class UpdateNoteDto
{
    public double? X { get; set; }
    public double? Y { get; set; }
    public string? Text { get; set; }
    public string? Color { get; set; }
}
