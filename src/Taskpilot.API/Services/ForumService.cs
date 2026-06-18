using Microsoft.EntityFrameworkCore;
using Taskpilot.API.Common;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Forum;
using Taskpilot.API.Models;

namespace Taskpilot.API.Services;

/// <summary>
/// Handles forum business logic: creating and listing topics, opening a topic
/// with its replies, and posting replies.
/// </summary>
public class ForumService : IForumService
{
    private readonly TaskpilotDbContext _context;
    private readonly ILogger<ForumService> _logger;

    public ForumService(TaskpilotDbContext context, ILogger<ForumService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<TopicDetailDto>> CreateTopicAsync(Guid authorId, CreateTopicDto dto)
    {
        _logger.LogInformation("CreateTopic. AuthorId: {AuthorId}", authorId);

        try
        {
            var topic = new ForumTopic
            {
                Id = Guid.NewGuid(),
                Title = dto.Title.Trim(),
                Body = dto.Body.Trim(),
                AuthorId = authorId,
                CreatedAt = DateTime.UtcNow,
            };
            _context.ForumTopics.Add(topic);
            await _context.SaveChangesAsync();

            // Author name for the response (a brand-new topic has no replies/views yet).
            var authorName = await _context.Users
                .Where(u => u.Id == authorId)
                .Select(u => u.Name)
                .FirstAsync();

            _logger.LogInformation("Topic created. TopicId: {TopicId}", topic.Id);
            return Result<TopicDetailDto>.Ok(new TopicDetailDto
            {
                Id = topic.Id,
                Title = topic.Title,
                Body = topic.Body,
                AuthorId = authorId,
                AuthorName = authorName,
                ViewCount = 0,
                IsPinned = false,
                IsLocked = false,
                CreatedAt = topic.CreatedAt,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating topic. AuthorId: {AuthorId}", authorId);
            return Result<TopicDetailDto>.Fail("An unexpected error occurred.");
        }
    }

    /// <inheritdoc />
    public async Task<Result<List<TopicListItemDto>>> GetTopicsAsync()
    {
        var topics = await _context.ForumTopics
            .OrderByDescending(t => t.IsPinned)
            .ThenByDescending(t => t.CreatedAt)
            .Select(t => new TopicListItemDto
            {
                Id = t.Id,
                Title = t.Title,
                AuthorId = t.AuthorId,
                AuthorName = t.Author.Name,
                ViewCount = t.ViewCount,
                ReplyCount = t.Replies.Count,
                IsPinned = t.IsPinned,
                IsLocked = t.IsLocked,
                CreatedAt = t.CreatedAt,
            })
            .AsNoTracking()
            .ToListAsync();

        return Result<List<TopicListItemDto>>.Ok(topics);
    }

    /// <inheritdoc />
    public async Task<Result<TopicDetailDto>> GetTopicAsync(Guid topicId)
    {
        var topic = await _context.ForumTopics
            .Include(t => t.Author)
            .Include(t => t.Replies).ThenInclude(r => r.Author)
            .FirstOrDefaultAsync(t => t.Id == topicId);

        if (topic is null)
            return Result<TopicDetailDto>.Fail("Topic not found.");

        // Count this read as a view.
        topic.ViewCount++;
        await _context.SaveChangesAsync();

        return Result<TopicDetailDto>.Ok(MapDetail(topic));
    }

    /// <inheritdoc />
    public async Task<Result<ReplyDto>> AddReplyAsync(Guid authorId, CreateReplyDto dto)
    {
        _logger.LogInformation("AddReply. TopicId: {TopicId}, AuthorId: {AuthorId}", dto.TopicId, authorId);

        var topic = await _context.ForumTopics.FirstOrDefaultAsync(t => t.Id == dto.TopicId);
        if (topic is null)
            return Result<ReplyDto>.Fail("Topic not found.");

        if (topic.IsLocked)
            return Result<ReplyDto>.Fail("This topic is locked.");

        // If replying to another reply, it must belong to the same topic.
        if (dto.ParentReplyId.HasValue)
        {
            var parentValid = await _context.ForumReplies
                .AnyAsync(r => r.Id == dto.ParentReplyId.Value && r.TopicId == dto.TopicId);
            if (!parentValid)
                return Result<ReplyDto>.Fail("Parent reply not found in this topic.");
        }

        try
        {
            var reply = new ForumReply
            {
                Id = Guid.NewGuid(),
                TopicId = dto.TopicId,
                AuthorId = authorId,
                Body = dto.Body.Trim(),
                ParentReplyId = dto.ParentReplyId,
                CreatedAt = DateTime.UtcNow,
            };
            _context.ForumReplies.Add(reply);
            await _context.SaveChangesAsync();

            var authorName = await _context.Users
                .Where(u => u.Id == authorId)
                .Select(u => u.Name)
                .FirstAsync();

            _logger.LogInformation("Reply added. ReplyId: {ReplyId}", reply.Id);
            return Result<ReplyDto>.Ok(MapReply(reply, authorName));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding reply. TopicId: {TopicId}", dto.TopicId);
            return Result<ReplyDto>.Fail("An unexpected error occurred.");
        }
    }

    // --- mapping ---

    private static TopicDetailDto MapDetail(ForumTopic t) => new()
    {
        Id = t.Id,
        Title = t.Title,
        Body = t.Body,
        AuthorId = t.AuthorId,
        AuthorName = t.Author?.Name ?? string.Empty,
        ViewCount = t.ViewCount,
        IsPinned = t.IsPinned,
        IsLocked = t.IsLocked,
        CreatedAt = t.CreatedAt,
        UpdatedAt = t.UpdatedAt,
        Replies = t.Replies
            .OrderBy(r => r.CreatedAt)
            .Select(r => MapReply(r, r.Author?.Name ?? string.Empty))
            .ToList(),
    };

    private static ReplyDto MapReply(ForumReply r, string authorName) => new()
    {
        Id = r.Id,
        TopicId = r.TopicId,
        AuthorId = r.AuthorId,
        AuthorName = authorName,
        Body = r.Body,
        ParentReplyId = r.ParentReplyId,
        IsSolution = r.IsSolution,
        CreatedAt = r.CreatedAt,
        UpdatedAt = r.UpdatedAt,
    };
}
