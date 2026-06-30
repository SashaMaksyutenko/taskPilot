namespace Taskpilot.API.Models;

/// <summary>
/// A collaborator on a project. Members share access to the board, tasks and comments;
/// the owner alone manages members and archives/deletes the project.
/// </summary>
public class ProjectMember
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Project the membership grants access to (foreign key).</summary>
    public Guid ProjectId { get; set; }

    /// <summary>Navigation to the project.</summary>
    public Project Project { get; set; } = null!;

    /// <summary>The collaborating user (foreign key).</summary>
    public Guid UserId { get; set; }

    /// <summary>Navigation to the member user.</summary>
    public User User { get; set; } = null!;

    /// <summary>UTC time the user was added to the project.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
