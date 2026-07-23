namespace Taskpilot.API.Models;

/// <summary>
/// A reusable project skeleton: a name plus a set of template tasks that can be stamped
/// out into a fresh project. Owned by the user who created it. Unlike a project it has no
/// board, members or chat — it is a blueprint, not a live workspace.
/// </summary>
public class ProjectTemplate
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Template name (defaults to the source project's name when saved).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional description carried onto projects created from this template.</summary>
    public string? Description { get; set; }

    /// <summary>Optional UI color carried onto created projects.</summary>
    public string? Color { get; set; }

    /// <summary>User who owns the template (foreign key).</summary>
    public Guid OwnerId { get; set; }

    /// <summary>Navigation to the owner.</summary>
    public User Owner { get; set; } = null!;

    /// <summary>UTC time the template was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>The tasks that make up this template.</summary>
    public ICollection<ProjectTemplateTask> Tasks { get; set; } = new List<ProjectTemplateTask>();
}
