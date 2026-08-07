namespace Taskpilot.API.Models;

/// <summary>
/// A project groups related tasks. Owned by a user; can be archived (soft) and restored.
/// </summary>
public class Project
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Project name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional description.</summary>
    public string? Description { get; set; }

    /// <summary>Optional UI color (e.g. hex) used to tag the project.</summary>
    public string? Color { get; set; }

    /// <summary>User who owns the project (foreign key).</summary>
    public Guid OwnerId { get; set; }

    /// <summary>Navigation to the owner.</summary>
    public User Owner { get; set; } = null!;

    /// <summary>UTC time the project was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>UTC time the project was archived; null while active.</summary>
    public DateTime? ArchivedAt { get; set; }

    /// <summary>
    /// When set, the board is viewable read-only (no login) at this opaque token. Null = not shared.
    /// The owner can generate or revoke it.
    /// </summary>
    public string? ShareToken { get; set; }

    /// <summary>Linked GitHub repository as "owner/name"; null when not connected.</summary>
    public string? GitHubRepo { get; set; }

    /// <summary>Secret used to verify inbound GitHub webhook signatures (HMAC-SHA256). Server-only.</summary>
    public string? GitHubWebhookSecret { get; set; }

    /// <summary>Tasks belonging to this project.</summary>
    public ICollection<ProjectTask> Tasks { get; set; } = new List<ProjectTask>();

    /// <summary>Collaborators with shared access to this project (besides the owner).</summary>
    public ICollection<ProjectMember> Members { get; set; } = new List<ProjectMember>();
}
