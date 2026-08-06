namespace Taskpilot.API.Models;

/// <summary>
/// Maps one of a user's tasks to the Google Calendar event created for it, so a re-sync
/// updates the same event instead of creating duplicates (and a future pull can match an
/// event back to its task). Scoped per user, since each user syncs to their own calendar.
/// </summary>
public class GoogleCalendarEventLink
{
    public Guid Id { get; set; }

    /// <summary>User whose calendar holds the event (foreign key).</summary>
    public Guid UserId { get; set; }

    /// <summary>Navigation to the owner.</summary>
    public User User { get; set; } = null!;

    /// <summary>The TaskPilot task this event mirrors (foreign key).</summary>
    public Guid TaskId { get; set; }

    /// <summary>Navigation to the task.</summary>
    public ProjectTask Task { get; set; } = null!;

    /// <summary>Id of the event in the user's Google Calendar.</summary>
    public string GoogleEventId { get; set; } = string.Empty;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
