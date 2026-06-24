namespace Taskpilot.API.Models;

/// <summary>
/// A personal note owned by a single user. Private to its owner.
/// </summary>
public class Note
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>User who owns the note (foreign key).</summary>
    public Guid OwnerId { get; set; }

    /// <summary>Navigation to the owner.</summary>
    public User Owner { get; set; } = null!;

    /// <summary>Short title/headline (may be empty).</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Note body.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Optional color tag (hex), used to color the card.</summary>
    public string? Color { get; set; }

    /// <summary>Pinned notes are shown first.</summary>
    public bool IsPinned { get; set; }

    /// <summary>UTC time the note was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>UTC time the note was last edited; null if never.</summary>
    public DateTime? UpdatedAt { get; set; }
}
