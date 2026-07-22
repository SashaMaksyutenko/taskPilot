namespace Taskpilot.API.Models;

/// <summary>
/// Links an uploaded file to a forum topic. A separate table rather than a column on
/// <see cref="ForumTopic"/> so a post can carry several files (logs, screenshots).
/// </summary>
public class ForumAttachment
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Topic the file is attached to (foreign key).</summary>
    public Guid TopicId { get; set; }

    /// <summary>Navigation to the topic.</summary>
    public ForumTopic Topic { get; set; } = null!;

    /// <summary>The stored file (foreign key).</summary>
    public Guid FileAttachmentId { get; set; }

    /// <summary>Navigation to the stored file.</summary>
    public FileAttachment FileAttachment { get; set; } = null!;

    /// <summary>Who attached the file — in practice the topic's author.</summary>
    public Guid UploadedById { get; set; }

    /// <summary>UTC time the file was attached.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
