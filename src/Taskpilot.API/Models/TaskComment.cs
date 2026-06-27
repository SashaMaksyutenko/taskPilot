namespace Taskpilot.API.Models;

/// <summary>
/// A comment left on a project task. Comments form a simple flat thread ordered by
/// creation time. Reachable only through a task whose project the caller owns.
/// </summary>
public class TaskComment
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Task the comment belongs to (foreign key).</summary>
    public Guid TaskId { get; set; }

    /// <summary>Navigation to the task.</summary>
    public ProjectTask Task { get; set; } = null!;

    /// <summary>User who wrote the comment (foreign key).</summary>
    public Guid AuthorId { get; set; }

    /// <summary>Navigation to the author.</summary>
    public User Author { get; set; } = null!;

    /// <summary>Comment text.</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>UTC time the comment was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>UTC time the comment was last edited; null if never.</summary>
    public DateTime? UpdatedAt { get; set; }
}
