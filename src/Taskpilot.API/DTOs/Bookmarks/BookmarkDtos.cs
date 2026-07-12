namespace Taskpilot.API.DTOs.Bookmarks;

/// <summary>Input for creating/toggling a bookmark.</summary>
public class ToggleBookmarkDto
{
    /// <summary>"Task", "Topic" or "Message".</summary>
    public string Type { get; set; } = string.Empty;

    public Guid EntityId { get; set; }

    /// <summary>Display title to snapshot.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>In-app route to open the entity.</summary>
    public string Link { get; set; } = string.Empty;
}

/// <summary>A bookmark as returned to clients.</summary>
public class BookmarkDto
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Link { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
