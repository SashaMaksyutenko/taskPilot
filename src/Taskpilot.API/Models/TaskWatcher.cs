namespace Taskpilot.API.Models;

/// <summary>
/// A user subscribed to ("watching") a task: they receive task notifications — e.g. status
/// changes — even when they are not the assignee. Watching is opt-in and independent of the
/// existing assignee/creator notifications.
/// </summary>
public class TaskWatcher
{
    public Guid Id { get; set; }

    /// <summary>The watched task.</summary>
    public Guid TaskId { get; set; }

    /// <summary>Navigation to the watched task.</summary>
    public ProjectTask Task { get; set; } = null!;

    /// <summary>The subscribed user.</summary>
    public Guid UserId { get; set; }

    /// <summary>Navigation to the subscribed user.</summary>
    public User User { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
