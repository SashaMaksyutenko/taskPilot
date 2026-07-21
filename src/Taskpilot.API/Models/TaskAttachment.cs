namespace Taskpilot.API.Models;

/// <summary>
/// Links an uploaded file to a project task. A task can carry many attachments, and the
/// same file row is never shared between tasks — detaching therefore deletes the file
/// itself rather than leaving orphaned bytes in storage.
/// </summary>
public class TaskAttachment
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Task the file is attached to (foreign key).</summary>
    public Guid TaskId { get; set; }

    /// <summary>Navigation to the task.</summary>
    public ProjectTask Task { get; set; } = null!;

    /// <summary>The uploaded file's metadata (foreign key).</summary>
    public Guid FileAttachmentId { get; set; }

    /// <summary>Navigation to the file.</summary>
    public FileAttachment FileAttachment { get; set; } = null!;

    /// <summary>Who attached it — not necessarily the task's assignee.</summary>
    public Guid UploadedById { get; set; }

    /// <summary>UTC time the file was attached.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
