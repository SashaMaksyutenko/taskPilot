using Microsoft.EntityFrameworkCore;
using Taskpilot.API.Common;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Forum;
using Taskpilot.API.Models;

namespace Taskpilot.API.Services;

/// <summary>
/// Files attached to forum topics. Uploading reuses <see cref="IFileService"/>, so the
/// organization's per-file limit and storage quota apply here exactly as they do to task
/// and chat attachments.
/// </summary>
public class ForumAttachmentService : IForumAttachmentService
{
    private readonly TaskpilotDbContext _context;
    private readonly IFileService _files;
    private readonly ILogger<ForumAttachmentService> _logger;

    public ForumAttachmentService(
        TaskpilotDbContext context,
        IFileService files,
        ILogger<ForumAttachmentService> logger)
    {
        _context = context;
        _files = files;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<ForumAttachmentDto>> AttachAsync(Guid userId, Guid topicId, IFormFile file)
    {
        _logger.LogInformation("AttachAsync started. TopicId: {TopicId}, UserId: {UserId}", topicId, userId);

        var topic = await _context.ForumTopics.AsNoTracking()
            .Where(t => t.Id == topicId)
            .Select(t => new { t.AuthorId, t.IsLocked })
            .FirstOrDefaultAsync();
        if (topic is null)
            return Result<ForumAttachmentDto>.Fail("Topic not found.");

        // Attaching is editing your own post: anyone else could otherwise hang files
        // off a stranger's topic, and they would be shown as part of it.
        if (topic.AuthorId != userId)
            return Result<ForumAttachmentDto>.Fail("You can only attach files to your own topics.");

        // A locked topic takes no new replies, so it takes no new files either.
        if (topic.IsLocked)
            return Result<ForumAttachmentDto>.Fail("This topic is locked.");

        // Muted users are blocked from every forum write path, this one included.
        if (await MuteGuard.CheckAsync(_context, userId) is { } muted)
            return Result<ForumAttachmentDto>.Fail(muted);

        // Reuses the shared upload path, so the size limit and the org storage quota are
        // enforced here too rather than being re-implemented.
        var saved = await _files.SaveAsync(file, userId);
        if (!saved.Succeeded)
            return Result<ForumAttachmentDto>.Fail(saved.Error!);

        var link = new ForumAttachment
        {
            Id = Guid.NewGuid(),
            TopicId = topicId,
            FileAttachmentId = saved.Value!.Id,
            UploadedById = userId,
            CreatedAt = DateTime.UtcNow,
        };
        _context.ForumAttachments.Add(link);
        await _context.SaveChangesAsync();

        _logger.LogInformation("File attached to topic. TopicId: {TopicId}, FileId: {FileId}", topicId, saved.Value.Id);

        var uploaderName = await _context.Users.AsNoTracking()
            .Where(u => u.Id == userId).Select(u => u.Name).FirstOrDefaultAsync();

        return Result<ForumAttachmentDto>.Ok(new ForumAttachmentDto
        {
            Id = link.Id,
            FileId = saved.Value.Id,
            FileName = saved.Value.FileName,
            ContentType = saved.Value.ContentType,
            SizeBytes = saved.Value.SizeBytes,
            UploadedById = userId,
            UploadedByName = uploaderName,
            CreatedAt = link.CreatedAt,
        });
    }

    /// <inheritdoc />
    public async Task<Result<List<ForumAttachmentDto>>> GetForTopicAsync(Guid topicId)
    {
        // The forum is open to every signed-in user, so reading needs no further check
        // beyond the topic existing.
        if (!await _context.ForumTopics.AnyAsync(t => t.Id == topicId))
            return Result<List<ForumAttachmentDto>>.Fail("Topic not found.");

        // Left join on the uploader so an attachment survives its uploader's deletion.
        var items = await _context.ForumAttachments
            .AsNoTracking()
            .Where(a => a.TopicId == topicId)
            .OrderByDescending(a => a.CreatedAt)
            .Join(_context.FileAttachments.AsNoTracking(),
                  a => a.FileAttachmentId, f => f.Id,
                  (a, f) => new { Link = a, File = f })
            .GroupJoin(_context.Users.AsNoTracking(),
                       x => x.Link.UploadedById, u => u.Id,
                       (x, users) => new { x.Link, x.File, Users = users })
            .SelectMany(x => x.Users.DefaultIfEmpty(),
                        (x, user) => new ForumAttachmentDto
                        {
                            Id = x.Link.Id,
                            FileId = x.File.Id,
                            FileName = x.File.FileName,
                            ContentType = x.File.ContentType,
                            SizeBytes = x.File.SizeBytes,
                            UploadedById = x.Link.UploadedById,
                            UploadedByName = user != null ? user.Name : null,
                            CreatedAt = x.Link.CreatedAt,
                        })
            .ToListAsync();

        return Result<List<ForumAttachmentDto>>.Ok(items);
    }

    /// <inheritdoc />
    public async Task<Result> DetachAsync(Guid userId, Guid attachmentId)
    {
        var link = await _context.ForumAttachments.FirstOrDefaultAsync(a => a.Id == attachmentId);
        if (link is null)
            return Result.Fail("Attachment not found.");

        // Only whoever attached the file may remove it — the same rule the rest of the
        // file features use. An admin who needs it gone deletes the topic.
        if (link.UploadedById != userId)
            return Result.Fail("Only the person who attached this file can remove it.");

        // The link must go first: ForumAttachment holds a Restrict foreign key to the
        // file, so deleting the file while the link exists would be refused.
        var fileId = link.FileAttachmentId;
        _context.ForumAttachments.Remove(link);
        await _context.SaveChangesAsync();

        var deleted = await _files.DeleteAsync(fileId, userId);
        if (!deleted.Succeeded)
            _logger.LogWarning("Attachment unlinked but its file could not be deleted. FileId: {FileId}, Reason: {Reason}",
                fileId, deleted.Error);

        _logger.LogInformation("Forum attachment removed. AttachmentId: {AttachmentId}, By: {UserId}", attachmentId, userId);
        return Result.Ok();
    }

    /// <inheritdoc />
    public async Task DeleteAllForTopicAsync(Guid topicId)
    {
        var links = await _context.ForumAttachments.Where(a => a.TopicId == topicId).ToListAsync();
        if (links.Count == 0)
            return;

        // Links first (they hold a Restrict foreign key to the files), then the files.
        var uploaders = links.ToDictionary(a => a.FileAttachmentId, a => a.UploadedById);
        _context.ForumAttachments.RemoveRange(links);
        await _context.SaveChangesAsync();

        foreach (var (fileId, uploaderId) in uploaders)
        {
            // Deleting is uploader-scoped, so each file is removed as the person who
            // attached it — an admin deleting the topic must still clean up its files.
            var deleted = await _files.DeleteAsync(fileId, uploaderId);
            if (!deleted.Succeeded)
                _logger.LogWarning("Topic deleted but an attached file could not be removed. FileId: {FileId}, Reason: {Reason}",
                    fileId, deleted.Error);
        }

        _logger.LogInformation("Topic attachments cleaned up. TopicId: {TopicId}, Files: {Count}", topicId, uploaders.Count);
    }
}
