namespace Taskpilot.API.Models;

/// <summary>
/// A sticky note on a project's collaborative whiteboard. Unlike the CRDT-synced task description,
/// notes are authoritative server-side records so per-note permissions (only the author or the
/// project owner may delete one) can actually be enforced. Realtime updates are broadcast over
/// <c>WhiteboardHub</c>.
/// </summary>
public class WhiteboardNote
{
    public Guid Id { get; set; }

    /// <summary>Project the note belongs to (foreign key).</summary>
    public Guid ProjectId { get; set; }

    public Project Project { get; set; } = null!;

    /// <summary>Position on the canvas.</summary>
    public double X { get; set; }
    public double Y { get; set; }

    public string Text { get; set; } = string.Empty;

    /// <summary>Sticky-note colour (hex).</summary>
    public string Color { get; set; } = "#fde68a";

    /// <summary>Who created the note — the only one (besides the project owner) who may delete it.</summary>
    public Guid AuthorId { get; set; }

    /// <summary>Author's display name, denormalized so a deleted account still shows attribution.</summary>
    public string AuthorName { get; set; } = string.Empty;

    /// <summary>Who last edited the text (null until someone other than the author edits).</summary>
    public Guid? EditedById { get; set; }
    public string? EditedByName { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
