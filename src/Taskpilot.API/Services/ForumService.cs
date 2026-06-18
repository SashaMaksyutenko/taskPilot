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
    public async Task<Result<TopicDetailDto>> GetTopicAsync(Guid topicId, Guid currentUserId)
    {
        var topic = await _context.ForumTopics
            .Include(t => t.Author)
            .Include(t => t.Replies).ThenInclude(r => r.Author)
            .Include(t => t.Replies).ThenInclude(r => r.Votes)
            .FirstOrDefaultAsync(t => t.Id == topicId);

        if (topic is null)
            return Result<TopicDetailDto>.Fail("Topic not found.");

        // Count this read as a view.
        topic.ViewCount++;
        await _context.SaveChangesAsync();

        return Result<TopicDetailDto>.Ok(MapDetail(topic, currentUserId));
    }

    /// <inheritdoc />
    public async Task<Result<VoteResultDto>> VoteReplyAsync(Guid userId, Guid replyId, int value)
    {
        if (value != 1 && value != -1)
            return Result<VoteResultDto>.Fail("Vote value must be +1 or -1.");

        var reply = await _context.ForumReplies
            .Include(r => r.Votes)
            .FirstOrDefaultAsync(r => r.Id == replyId);
        if (reply is null)
            return Result<VoteResultDto>.Fail("Reply not found.");

        var existing = reply.Votes.FirstOrDefault(v => v.UserId == userId);
        if (existing is null)
        {
            // First vote by this user on this reply. Add through the DbSet so EF
            // marks it as Added (adding to the nav collection with a preset Guid key
            // would otherwise be treated as an existing row -> wrong UPDATE).
            _context.ForumVotes.Add(new ForumVote
            {
                Id = Guid.NewGuid(),
                ReplyId = replyId,
                UserId = userId,
                Value = value,
                CreatedAt = DateTime.UtcNow,
            });
        }
        else if (existing.Value == value)
        {
            // Same vote again -> toggle it off.
            _context.ForumVotes.Remove(existing);
        }
        else
        {
            // Switch upvote <-> downvote.
            existing.Value = value;
            existing.CreatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        // Recompute from the database to reflect the change.
        var votes = await _context.ForumVotes.Where(v => v.ReplyId == replyId).ToListAsync();
        var myVote = votes.FirstOrDefault(v => v.UserId == userId)?.Value ?? 0;

        return Result<VoteResultDto>.Ok(new VoteResultDto
        {
            ReplyId = replyId,
            Score = votes.Sum(v => v.Value),
            MyVote = myVote,
        });
    }

    /// <inheritdoc />
    public async Task<Result> MarkSolutionAsync(Guid userId, Guid replyId)
    {
        var reply = await _context.ForumReplies
            .Include(r => r.Topic)
            .FirstOrDefaultAsync(r => r.Id == replyId);
        if (reply is null)
            return Result.Fail("Reply not found.");

        // Only the topic's author may accept a solution.
        if (reply.Topic.AuthorId != userId)
            return Result.Fail("Only the topic author can mark a solution.");

        // Clear any previous solution in this topic, then mark this one.
        var topicReplies = await _context.ForumReplies
            .Where(r => r.TopicId == reply.TopicId)
            .ToListAsync();
        foreach (var r in topicReplies)
            r.IsSolution = r.Id == replyId;

        await _context.SaveChangesAsync();
        _logger.LogInformation("Solution marked. TopicId: {TopicId}, ReplyId: {ReplyId}", reply.TopicId, replyId);
        return Result.Ok();
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
            // A brand-new reply has no votes yet.
            return Result<ReplyDto>.Ok(new ReplyDto
            {
                Id = reply.Id,
                TopicId = reply.TopicId,
                AuthorId = authorId,
                AuthorName = authorName,
                Body = reply.Body,
                ParentReplyId = reply.ParentReplyId,
                IsSolution = false,
                Score = 0,
                MyVote = 0,
                CreatedAt = reply.CreatedAt,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding reply. TopicId: {TopicId}", dto.TopicId);
            return Result<ReplyDto>.Fail("An unexpected error occurred.");
        }
    }

    // --- mapping ---

    private static TopicDetailDto MapDetail(ForumTopic t, Guid currentUserId) => new()
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
            .Select(r => MapReply(r, currentUserId))
            .ToList(),
    };

    private static ReplyDto MapReply(ForumReply r, Guid currentUserId) => new()
    {
        Id = r.Id,
        TopicId = r.TopicId,
        AuthorId = r.AuthorId,
        AuthorName = r.Author?.Name ?? string.Empty,
        Body = r.Body,
        ParentReplyId = r.ParentReplyId,
        IsSolution = r.IsSolution,
        Score = r.Votes?.Sum(v => v.Value) ?? 0,
        MyVote = r.Votes?.FirstOrDefault(v => v.UserId == currentUserId)?.Value ?? 0,
        CreatedAt = r.CreatedAt,
        UpdatedAt = r.UpdatedAt,
    };
}
