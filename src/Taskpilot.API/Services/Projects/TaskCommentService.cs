using Microsoft.EntityFrameworkCore;
using Taskpilot.API.Common;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Projects;
using Taskpilot.API.Mappers;
using Taskpilot.API.Models;

namespace Taskpilot.API.Services;

/// <summary>
/// Handles task-comment business logic. A task is reachable only through a project
/// the caller owns, so reads and writes are scoped through that ownership.
/// </summary>
public class TaskCommentService : ITaskCommentService
{
    private readonly TaskpilotDbContext _context;
    private readonly IWebhookService _webhooks;
    private readonly INotificationService _notifications;
    private readonly ILogger<TaskCommentService> _logger;

    public TaskCommentService(
        TaskpilotDbContext context,
        IWebhookService webhooks,
        INotificationService notifications,
        ILogger<TaskCommentService> logger)
    {
        _context = context;
        _webhooks = webhooks;
        _notifications = notifications;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<List<TaskCommentDto>>> GetForTaskAsync(Guid userId, Guid taskId)
    {
        if (!await OwnsTaskAsync(taskId, userId))
            return Result<List<TaskCommentDto>>.Fail("Task not found.");

        var rows = await _context.TaskComments
            .Where(c => c.TaskId == taskId)
            .OrderBy(c => c.CreatedAt)
            .Select(c => new CommentRow(c.Id, c.TaskId, c.AuthorId, c.Author.Name, c.Author.AvatarFileId, c.Body, c.CreatedAt, c.UpdatedAt))
            .AsNoTracking()
            .ToListAsync();

        return Result<List<TaskCommentDto>>.Ok(rows.Select(MapDto).ToList());
    }

    // Intermediate row materialized from SQL; the avatar URL is composed in memory.
    private record CommentRow(
        Guid Id, Guid TaskId, Guid AuthorId, string AuthorName, Guid? AuthorAvatarFileId,
        string Body, DateTime CreatedAt, DateTime? UpdatedAt);

    private static TaskCommentDto MapDto(CommentRow c) => new()
    {
        Id = c.Id,
        TaskId = c.TaskId,
        AuthorId = c.AuthorId,
        AuthorName = c.AuthorName,
        AuthorAvatarUrl = UserMapper.AvatarUrl(c.AuthorId, c.AuthorAvatarFileId),
        Body = c.Body,
        CreatedAt = c.CreatedAt,
        UpdatedAt = c.UpdatedAt,
    };

    /// <inheritdoc />
    public async Task<Result<TaskCommentDto>> AddAsync(Guid userId, Guid taskId, CreateCommentDto dto)
    {
        if (!await OwnsTaskAsync(taskId, userId))
            return Result<TaskCommentDto>.Fail("Task not found.");

        if (!await ProjectAccess.CanWriteTaskAsync(_context, taskId, userId))
            return Result<TaskCommentDto>.Fail("You have read-only access to this project.");

        if (await MuteGuard.CheckAsync(_context, userId) is { } muted)
            return Result<TaskCommentDto>.Fail(muted);

        var comment = new TaskComment
        {
            Id = Guid.NewGuid(),
            TaskId = taskId,
            AuthorId = userId,
            Body = dto.Body.Trim(),
            CreatedAt = DateTime.UtcNow,
        };
        _context.TaskComments.Add(comment);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Comment added. TaskId: {TaskId}, CommentId: {CommentId}", taskId, comment.Id);

        // Reload through the projection so AuthorName/avatar are populated.
        var added = await _context.TaskComments
            .Where(c => c.Id == comment.Id)
            .Select(c => new CommentRow(c.Id, c.TaskId, c.AuthorId, c.Author.Name, c.Author.AvatarFileId, c.Body, c.CreatedAt, c.UpdatedAt))
            .AsNoTracking()
            .FirstAsync();

        await _webhooks.DispatchAsync(WebhookEvents.CommentCreated, new
        {
            commentId = comment.Id,
            taskId,
            authorId = userId,
            body = comment.Body,
        });

        // Gather the audience (owner + members) with names, so we can resolve @mentions.
        var ctx = await _context.ProjectTasks
            .Where(t => t.Id == taskId)
            .Select(t => new
            {
                t.ProjectId,
                t.Title,
                Owner = new { t.Project.OwnerId, Name = t.Project.Owner.Name },
                Members = t.Project.Members.Select(m => new { m.UserId, m.User.Name }).ToList(),
            })
            .FirstAsync();

        var audience = ctx.Members
            .Select(m => (Id: m.UserId, m.Name))
            .Append((Id: ctx.Owner.OwnerId, ctx.Owner.Name))
            .Where(a => a.Id != userId)
            .DistinctBy(a => a.Id)
            .ToList();

        // Resolve @mentions among the audience.
        var mentioned = MentionParser.Extract(comment.Body, audience);

        foreach (var (id, _) in audience)
        {
            var isMention = mentioned.Contains(id);
            await _notifications.CreateAsync(
                id,
                NotificationType.Task,
                isMention
                    ? $"{added.AuthorName} mentioned you in \"{ctx.Title}\"."
                    : $"{added.AuthorName} commented on \"{ctx.Title}\".",
                $"/projects/{ctx.ProjectId}");
        }

        if (mentioned.Count > 0)
            await _webhooks.DispatchAsync(WebhookEvents.MentionTriggered, new
            {
                source = "task-comment",
                taskId,
                projectId = ctx.ProjectId,
                authorId = userId,
                mentionedUserIds = mentioned,
            });

        return Result<TaskCommentDto>.Ok(MapDto(added));
    }

    /// <inheritdoc />
    public async Task<Result<Guid>> DeleteAsync(Guid userId, Guid commentId)
    {
        var comment = await _context.TaskComments.FirstOrDefaultAsync(c => c.Id == commentId);
        if (comment is null)
            return Result<Guid>.Fail("Comment not found.");

        // Only the author may delete their own comment.
        if (comment.AuthorId != userId)
            return Result<Guid>.Fail("You can only delete your own comments.");

        var taskId = comment.TaskId;
        _context.TaskComments.Remove(comment);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Comment deleted. CommentId: {CommentId}", commentId);
        return Result<Guid>.Ok(taskId);
    }

    /// <summary>True if the task exists and the caller owns or collaborates on its project.</summary>
    private Task<bool> OwnsTaskAsync(Guid taskId, Guid userId) =>
        _context.ProjectTasks.AnyAsync(t => t.Id == taskId &&
            (t.Project.OwnerId == userId || t.Project.Members.Any(m => m.UserId == userId)));
}
