namespace Taskpilot.API.Models;

/// <summary>
/// A user's appeal against a moderation warning. Reviewed by an admin who approves
/// (which removes the linked warning) or rejects it.
/// </summary>
public class Appeal
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>User who filed the appeal (foreign key).</summary>
    public Guid UserId { get; set; }

    /// <summary>Navigation to the appealing user.</summary>
    public User User { get; set; } = null!;

    /// <summary>Optional warning being appealed; null if cleared/general.</summary>
    public Guid? WarningId { get; set; }

    /// <summary>Navigation to the appealed warning.</summary>
    public UserWarning? Warning { get; set; }

    /// <summary>The user's explanation of why the warning should be lifted.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Review state.</summary>
    public AppealStatus Status { get; set; } = AppealStatus.Pending;

    /// <summary>Admin who reviewed the appeal (foreign key); null until reviewed.</summary>
    public Guid? ReviewedById { get; set; }

    /// <summary>Navigation to the reviewing admin.</summary>
    public User? ReviewedBy { get; set; }

    /// <summary>Optional note the admin left when resolving the appeal.</summary>
    public string? ReviewNote { get; set; }

    /// <summary>UTC time the appeal was filed.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>UTC time the appeal was resolved; null while pending.</summary>
    public DateTime? ReviewedAt { get; set; }
}
