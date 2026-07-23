using Microsoft.EntityFrameworkCore;
using Taskpilot.API.Common;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Files;
using Taskpilot.API.DTOs.Projects;
using Taskpilot.API.Models;

namespace Taskpilot.API.Services;

/// <summary>
/// Files attached to project tasks. Uploading reuses <see cref="IFileService"/>, so the
/// organization's per-file limit and storage quota apply here exactly as they do to chat
/// attachments and avatars.
/// </summary>
public class TaskAttachmentService : ITaskAttachmentService
{
    private readonly TaskpilotDbContext _context;
    private readonly IFileService _files;
    private readonly ILogger<TaskAttachmentService> _logger;

    public TaskAttachmentService(
        TaskpilotDbContext context,
        IFileService files,
        ILogger<TaskAttachmentService> logger)
    {
        _context = context;
        _files = files;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<TaskAttachmentDto>> AttachAsync(Guid userId, Guid taskId, IFormFile file)
    {
        _logger.LogInformation("AttachAsync started. TaskId: {TaskId}, UserId: {UserId}", taskId, userId);

        if (!await TaskExistsForAsync(taskId, userId))
            return Result<TaskAttachmentDto>.Fail("Task not found.");
        if (!await ProjectAccess.CanWriteTaskAsync(_context, taskId, userId))
            return Result<TaskAttachmentDto>.Fail("You have read-only access to this project.");

        // Reuses the shared upload path, so the size limit and the org storage quota are
        // enforced here too rather than being re-implemented.
        var saved = await _files.SaveAsync(file, userId);
        if (!saved.Succeeded)
            return Result<TaskAttachmentDto>.Fail(saved.Error!);

        var link = new TaskAttachment
        {
            Id = Guid.NewGuid(),
            TaskId = taskId,
            FileAttachmentId = saved.Value!.Id,
            UploadedById = userId,
            CreatedAt = DateTime.UtcNow,
        };
        _context.TaskAttachments.Add(link);
        await _context.SaveChangesAsync();

        _logger.LogInformation("File attached to task. TaskId: {TaskId}, FileId: {FileId}", taskId, saved.Value.Id);

        var uploaderName = await _context.Users.AsNoTracking()
            .Where(u => u.Id == userId).Select(u => u.Name).FirstOrDefaultAsync();

        return Result<TaskAttachmentDto>.Ok(new TaskAttachmentDto
        {
            Id = link.Id,
            FileId = saved.Value.Id,
            FileName = saved.Value.FileName,
            ContentType = saved.Value.ContentType,
            SizeBytes = saved.Value.SizeBytes,
            Version = 1,
            UploadedById = userId,
            UploadedByName = uploaderName,
            CreatedAt = link.CreatedAt,
        });
    }

    /// <inheritdoc />
    public async Task<Result<TaskAttachmentDto>> UploadVersionAsync(Guid userId, Guid attachmentId, IFormFile file)
    {
        var link = await _context.TaskAttachments.FirstOrDefaultAsync(a => a.Id == attachmentId);
        if (link is null)
            return Result<TaskAttachmentDto>.Fail("Attachment not found.");
        if (!await TaskExistsForAsync(link.TaskId, userId))
            return Result<TaskAttachmentDto>.Fail("Attachment not found.");

        // Replacing a file is uploader-only, like detaching it: a new version supersedes the
        // old one for everyone, so only the person who attached it should get to decide.
        if (link.UploadedById != userId)
            return Result<TaskAttachmentDto>.Fail("Only the person who attached this file can upload a new version.");

        var saved = await _files.SaveVersionAsync(file, userId, link.FileAttachmentId);
        if (!saved.Succeeded)
            return Result<TaskAttachmentDto>.Fail(saved.Error!);

        // Point the attachment at the new head; the old version stays, chained behind it.
        link.FileAttachmentId = saved.Value!.Id;
        await _context.SaveChangesAsync();

        _logger.LogInformation("New attachment version uploaded. AttachmentId: {AttachmentId}, FileId: {FileId}, By: {UserId}",
            attachmentId, saved.Value.Id, userId);

        var uploaderName = await _context.Users.AsNoTracking()
            .Where(u => u.Id == userId).Select(u => u.Name).FirstOrDefaultAsync();
        var newVersion = await _context.FileAttachments.AsNoTracking()
            .Where(f => f.Id == saved.Value.Id).Select(f => f.Version).FirstAsync();

        return Result<TaskAttachmentDto>.Ok(new TaskAttachmentDto
        {
            Id = link.Id,
            FileId = saved.Value.Id,
            FileName = saved.Value.FileName,
            ContentType = saved.Value.ContentType,
            SizeBytes = saved.Value.SizeBytes,
            Version = newVersion,
            UploadedById = userId,
            UploadedByName = uploaderName,
            CreatedAt = link.CreatedAt,
        });
    }

    /// <inheritdoc />
    public async Task<Result<List<FileVersionDto>>> GetVersionsAsync(Guid userId, Guid attachmentId)
    {
        var link = await _context.TaskAttachments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == attachmentId);
        if (link is null)
            return Result<List<FileVersionDto>>.Fail("Attachment not found.");

        // Reading the history only needs access to the task's project — Viewers included.
        if (!await TaskExistsForAsync(link.TaskId, userId))
            return Result<List<FileVersionDto>>.Fail("Attachment not found.");

        return await _files.GetVersionsAsync(link.FileAttachmentId);
    }

    /// <inheritdoc />
    public async Task<Result<List<TaskAttachmentDto>>> GetForTaskAsync(Guid userId, Guid taskId)
    {
        // Reading only needs access to the project — Viewers included.
        if (!await TaskExistsForAsync(taskId, userId))
            return Result<List<TaskAttachmentDto>>.Fail("Task not found.");

        // Left join on the uploader so an attachment survives its uploader's deletion.
        var items = await _context.TaskAttachments
            .AsNoTracking()
            .Where(a => a.TaskId == taskId)
            .OrderByDescending(a => a.CreatedAt)
            .Join(_context.FileAttachments.AsNoTracking(),
                  a => a.FileAttachmentId, f => f.Id,
                  (a, f) => new { Link = a, File = f })
            .GroupJoin(_context.Users.AsNoTracking(),
                       x => x.Link.UploadedById, u => u.Id,
                       (x, users) => new { x.Link, x.File, Users = users })
            .SelectMany(x => x.Users.DefaultIfEmpty(),
                        (x, user) => new TaskAttachmentDto
                        {
                            Id = x.Link.Id,
                            FileId = x.File.Id,
                            FileName = x.File.FileName,
                            ContentType = x.File.ContentType,
                            SizeBytes = x.File.SizeBytes,
                            Version = x.File.Version,
                            UploadedById = x.Link.UploadedById,
                            UploadedByName = user != null ? user.Name : null,
                            CreatedAt = x.Link.CreatedAt,
                        })
            .ToListAsync();

        return Result<List<TaskAttachmentDto>>.Ok(items);
    }

    /// <inheritdoc />
    public async Task<Result> DetachAsync(Guid userId, Guid attachmentId)
    {
        var link = await _context.TaskAttachments.FirstOrDefaultAsync(a => a.Id == attachmentId);
        if (link is null)
            return Result.Fail("Attachment not found.");

        if (!await TaskExistsForAsync(link.TaskId, userId))
            return Result.Fail("Attachment not found.");

        // Only whoever attached the file may remove it — the same rule the rest of the file
        // features use (sharing and deleting are uploader-only too). Anyone else who needs
        // it gone can delete the task itself.
        if (link.UploadedById != userId)
            return Result.Fail("Only the person who attached this file can remove it.");

        // The link must go first: TaskAttachment holds a Restrict foreign key to the file,
        // so deleting the file while the link exists would be refused by the database.
        var fileId = link.FileAttachmentId;
        _context.TaskAttachments.Remove(link);
        await _context.SaveChangesAsync();

        // Now the file and every earlier version, so detaching never leaves orphaned rows
        // or bytes in storage.
        var deleted = await _files.DeleteWithVersionsAsync(fileId, userId);
        if (!deleted.Succeeded)
            _logger.LogWarning("Attachment unlinked but its file could not be deleted. FileId: {FileId}, Reason: {Reason}",
                fileId, deleted.Error);

        _logger.LogInformation("Attachment removed. AttachmentId: {AttachmentId}, By: {UserId}", attachmentId, userId);
        return Result.Ok();
    }

    /// <inheritdoc />
    public async Task DeleteAllForTaskAsync(Guid taskId)
    {
        var links = await _context.TaskAttachments.Where(a => a.TaskId == taskId).ToListAsync();
        if (links.Count == 0)
            return;

        // Links first (they hold a Restrict foreign key to the files), then the files.
        var fileIds = links.Select(a => a.FileAttachmentId).ToList();
        var uploaders = links.ToDictionary(a => a.FileAttachmentId, a => a.UploadedById);
        _context.TaskAttachments.RemoveRange(links);
        await _context.SaveChangesAsync();

        foreach (var fileId in fileIds)
        {
            // Deleting is uploader-scoped, so each file (and its versions) is removed as the
            // person who attached it — the task's own deletion was already authorised.
            var deleted = await _files.DeleteWithVersionsAsync(fileId, uploaders[fileId]);
            if (!deleted.Succeeded)
                _logger.LogWarning("Task deleted but an attached file could not be removed. FileId: {FileId}, Reason: {Reason}",
                    fileId, deleted.Error);
        }

        _logger.LogInformation("Task attachments cleaned up. TaskId: {TaskId}, Files: {Count}", taskId, fileIds.Count);
    }

    /// <summary>True when the task exists and the user can reach it through its project.</summary>
    private Task<bool> TaskExistsForAsync(Guid taskId, Guid userId) =>
        _context.ProjectTasks.AnyAsync(t => t.Id == taskId &&
            (t.Project.OwnerId == userId || t.Project.Members.Any(m => m.UserId == userId)));
}
