using Taskpilot.API.Common;
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
