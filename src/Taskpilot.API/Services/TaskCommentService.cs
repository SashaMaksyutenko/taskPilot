using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Taskpilot.API.Common;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Projects;
using Taskpilot.API.Models;

namespace Taskpilot.API.Services;

/// <summary>
/// Handles task-comment business logic. A task is reachable only through a project
/// the caller owns, so reads and writes are scoped through that ownership.
/// </summary>
public class TaskCommentService : ITaskCommentService
{
    private readonly TaskpilotDbContext _context;
    private readonly ILogger<TaskCommentService> _logger;

    public TaskCommentService(TaskpilotDbContext context, ILogger<TaskCommentService> logger)
    {
        _context = context;
        _logger = logger;
    }

    // Reusable projection TaskComment -> TaskCommentDto (runs in SQL).
    private static readonly Expression<Func<TaskComment, TaskCommentDto>> ToDto = c => new TaskCommentDto
    {
        Id = c.Id,
        TaskId = c.TaskId,
        AuthorId = c.AuthorId,
        AuthorName = c.Author.Name,
        Body = c.Body,
        CreatedAt = c.CreatedAt,
        UpdatedAt = c.UpdatedAt,
    };

    /// <inheritdoc />
    public async Task<Result<List<TaskCommentDto>>> GetForTaskAsync(Guid userId, Guid taskId)
    {
        if (!await OwnsTaskAsync(taskId, userId))
            return Result<List<TaskCommentDto>>.Fail("Task not found.");

        var comments = await _context.TaskComments
            .Where(c => c.TaskId == taskId)
            .OrderBy(c => c.CreatedAt)
            .Select(ToDto)
            .AsNoTracking()
            .ToListAsync();

        return Result<List<TaskCommentDto>>.Ok(comments);
    }

    /// <inheritdoc />
    public async Task<Result<TaskCommentDto>> AddAsync(Guid userId, Guid taskId, CreateCommentDto dto)
    {
        if (!await OwnsTaskAsync(taskId, userId))
            return Result<TaskCommentDto>.Fail("Task not found.");

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

        // Reload through the projection so AuthorName is populated.
        var added = await _context.TaskComments
            .Where(c => c.Id == comment.Id)
            .Select(ToDto)
            .AsNoTracking()
            .FirstAsync();

        return Result<TaskCommentDto>.Ok(added);
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(Guid userId, Guid commentId)
    {
        var comment = await _context.TaskComments.FirstOrDefaultAsync(c => c.Id == commentId);
        if (comment is null)
            return Result.Fail("Comment not found.");

        // Only the author may delete their own comment.
        if (comment.AuthorId != userId)
            return Result.Fail("You can only delete your own comments.");

        _context.TaskComments.Remove(comment);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Comment deleted. CommentId: {CommentId}", commentId);
        return Result.Ok();
    }

    /// <summary>True if the task exists and its project belongs to the caller.</summary>
    private Task<bool> OwnsTaskAsync(Guid taskId, Guid userId) =>
        _context.ProjectTasks.AnyAsync(t => t.Id == taskId && t.Project.OwnerId == userId);
}
