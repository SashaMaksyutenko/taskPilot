using Taskpilot.API.Common;
using Taskpilot.API.DTOs.Files;
using Taskpilot.API.DTOs.Projects;

namespace Taskpilot.API.Services;

/// <summary>
/// Files attached to project tasks (spec module 7: "upload to tasks/messages/forum posts").
/// Access follows the task's project: any participant may read, only those who can write to
/// the project may attach or detach.
/// </summary>
public interface ITaskAttachmentService
{
    /// <summary>Uploads a file and attaches it to the task.</summary>
    Task<Result<TaskAttachmentDto>> AttachAsync(Guid userId, Guid taskId, IFormFile file);

    /// <summary>Lists a task's attachments, newest first.</summary>
    Task<Result<List<TaskAttachmentDto>>> GetForTaskAsync(Guid userId, Guid taskId);

    /// <summary>
    /// Uploads a new version of an existing attachment: the new bytes become the current
    /// version and the previous one is kept as history. Only the person who created the
    /// attachment may replace it, matching detach being uploader-only.
    /// </summary>
    Task<Result<TaskAttachmentDto>> UploadVersionAsync(Guid userId, Guid attachmentId, IFormFile file);

    /// <summary>Lists an attachment's version history (newest first).</summary>
    Task<Result<List<FileVersionDto>>> GetVersionsAsync(Guid userId, Guid attachmentId);

    /// <summary>
    /// Removes an attachment and deletes the underlying file, so detaching never leaves
    /// orphaned bytes in storage.
    /// </summary>
    Task<Result> DetachAsync(Guid userId, Guid attachmentId);

    /// <summary>
    /// Deletes every attachment of a task and the files behind them. Called while deleting
    /// the task itself, so the caller has already checked permissions; without this the
    /// cascade would drop the links and leave the file rows and bytes behind forever.
    /// </summary>
    Task DeleteAllForTaskAsync(Guid taskId);
}
