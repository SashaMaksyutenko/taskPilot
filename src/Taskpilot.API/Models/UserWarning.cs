namespace Taskpilot.API.Models;

/// <summary>
/// A moderation warning issued to a user by an admin. Accumulated warnings drive
/// escalation: reaching the threshold auto-bans the account.
/// </summary>
public class UserWarning
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>User who received the warning (foreign key).</summary>
    public Guid UserId { get; set; }

    /// <summary>Navigation to the warned user.</summary>
    public User User { get; set; } = null!;

    /// <summary>Admin who issued the warning (foreign key).</summary>
    public Guid IssuedById { get; set; }

    /// <summary>Navigation to the issuing admin.</summary>
    public User IssuedBy { get; set; } = null!;

    /// <summary>Why the warning was issued.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>UTC time the warning was issued.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
