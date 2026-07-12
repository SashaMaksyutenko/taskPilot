namespace Taskpilot.API.Models;

/// <summary>The kind of entity a bookmark points to.</summary>
public enum BookmarkType
{
    Task,
    Topic,
    Message,
}

/// <summary>
/// A user's saved shortcut to a task, forum topic or chat message. The title and
/// link are snapshotted at save time so the quick-access list needs no joins.
/// </summary>
public class Bookmark
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Owner of the bookmark (foreign key).</summary>
    public Guid UserId { get; set; }

    /// <summary>Navigation to the owner.</summary>
    public User User { get; set; } = null!;

    /// <summary>What kind of entity this points to.</summary>
    public BookmarkType Type { get; set; }

    /// <summary>Id of the bookmarked entity.</summary>
    public Guid EntityId { get; set; }

    /// <summary>Display title captured when the bookmark was created.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>In-app route to open the entity (e.g. "/forum/{id}").</summary>
    public string Link { get; set; } = string.Empty;

    /// <summary>UTC time the bookmark was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
