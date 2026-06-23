namespace Taskpilot.API.Models;

/// <summary>
/// An immutable audit-trail entry: records that someone performed an action in the
/// system (e.g. logged in, banned a user, deleted a project). Audit logs must
/// survive even after the actor's account is removed, so the actor is stored by id
/// and an email snapshot — there is intentionally no foreign key to <see cref="User"/>.
/// </summary>
public class AuditLog
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Id of the user who performed the action; null for system/anonymous actions.</summary>
    public Guid? ActorId { get; set; }

    /// <summary>Email of the actor at the time of the action (snapshot for readability).</summary>
    public string? ActorEmail { get; set; }

    /// <summary>What happened, as a stable dotted code, e.g. "auth.login.success" or "user.banned".</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Type of the affected entity, if any (e.g. "User", "Project").</summary>
    public string? EntityType { get; set; }

    /// <summary>Id of the affected entity, if any (kept as string to fit any key type).</summary>
    public string? EntityId { get; set; }

    /// <summary>Optional free-form context (e.g. old/new value, reason).</summary>
    public string? Details { get; set; }

    /// <summary>Caller IP address, when available.</summary>
    public string? IpAddress { get; set; }

    /// <summary>UTC time the action occurred.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
