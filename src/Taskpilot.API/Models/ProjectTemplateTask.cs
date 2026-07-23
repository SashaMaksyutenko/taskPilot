namespace Taskpilot.API.Models;

/// <summary>
/// A task inside a <see cref="ProjectTemplate"/>. It captures only what belongs in a
/// blueprint — title, description, priority, tags, subtask structure and a RELATIVE
/// deadline — not per-instance state like status, assignee or an absolute due date.
/// </summary>
public class ProjectTemplateTask
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Template this task belongs to (foreign key).</summary>
    public Guid TemplateId { get; set; }

    /// <summary>Navigation to the template.</summary>
    public ProjectTemplate Template { get; set; } = null!;

    /// <summary>Task title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Optional details.</summary>
    public string? Description { get; set; }

    /// <summary>Priority stamped onto the created task.</summary>
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;

    /// <summary>
    /// Deadline expressed as a number of days from the created project's start date, so a
    /// template keeps its schedule relative ("due 5 days in") rather than pinned to an old
    /// absolute date. Null means the task has no deadline.
    /// </summary>
    public int? DeadlineOffsetDays { get; set; }

    /// <summary>Optional parent template task for subtasks; null for a top-level task.</summary>
    public Guid? ParentTemplateTaskId { get; set; }

    /// <summary>Navigation to the parent template task.</summary>
    public ProjectTemplateTask? ParentTemplateTask { get; set; }

    /// <summary>Free-form labels stamped onto the created task (stored as a Postgres text[]).</summary>
    public List<string> Tags { get; set; } = new();
}
