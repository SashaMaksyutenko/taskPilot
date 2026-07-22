using Taskpilot.API.Common;
using Taskpilot.API.DTOs.Forum;

namespace Taskpilot.API.Services;

/// <summary>
/// Files attached to forum topics — the last part of spec module 7
/// ("upload to tasks/messages/forum posts").
/// The forum is readable by every signed-in user, so anyone may list a topic's files;
/// only the topic's author may add or remove them.
/// </summary>
public interface IForumAttachmentService
{
    /// <summary>Uploads a file and attaches it to the topic (author only).</summary>
    Task<Result<ForumAttachmentDto>> AttachAsync(Guid userId, Guid topicId, IFormFile file);

    /// <summary>Lists a topic's attachments, newest first.</summary>
    Task<Result<List<ForumAttachmentDto>>> GetForTopicAsync(Guid topicId);

    /// <summary>
    /// Removes an attachment and deletes the underlying file, so detaching never leaves
    /// orphaned bytes in storage.
    /// </summary>
    Task<Result> DetachAsync(Guid userId, Guid attachmentId);

    /// <summary>
    /// Deletes every attachment of a topic and the files behind them. Called while the
    /// topic itself is being deleted (by its author or an admin), so the caller has
    /// already checked permissions.
    /// </summary>
    Task DeleteAllForTopicAsync(Guid topicId);
}
