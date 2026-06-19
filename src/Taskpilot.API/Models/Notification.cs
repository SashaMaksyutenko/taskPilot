namespace Taskpilot.API.Models;

/// <summary>
/// An in-app notification delivered to a single user (shown in the bell menu).
/// </summary>
public class Notification
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>User who receives the notification (foreign key).</summary>
    public Guid RecipientId { get; set; }

    /// <summary>Navigation to the recipient.</summary>
    public User Recipient { get; set; } = null!;

    /// <summary>Category (for icon/filtering).</summary>
    public NotificationType Type { get; set; }

    /// <summary>Human-readable message text.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Optional client route/URL to the related entity (e.g. a task or topic).</summary>
    public string? Link { get; set; }

    /// <summary>Whether the user has read it.</summary>
    public bool IsRead { get; set; }

    /// <summary>UTC time the notification was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
