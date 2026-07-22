using Microsoft.EntityFrameworkCore;
using Taskpilot.API.Common;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Chat;
using Taskpilot.API.DTOs.Common;
using Taskpilot.API.DTOs.Forum;
using Taskpilot.API.Mappers;
using Taskpilot.API.Models;

namespace Taskpilot.API.Services;

/// <summary>
/// Handles forum business logic: creating and listing topics, opening a topic
/// with its replies, and posting replies.
/// </summary>
public class ForumService : IForumService
{
    private readonly TaskpilotDbContext _context;
    private readonly INotificationService _notifications;
    private readonly IReputationService _reputation;
    private readonly IForumAttachmentService _attachments;
    private readonly ILogger<ForumService> _logger;

    public ForumService(
        TaskpilotDbContext context,
        INotificationService notifications,
        IReputationService reputation,
        IForumAttachmentService attachments,
        ILogger<ForumService> logger)
    {
        _context = context;
        _notifications = notifications;
        _reputation = reputation;
        _attachments = attachments;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<TopicDetailDto>> CreateTopicAsync(Guid authorId, CreateTopicDto dto)
    {
        _logger.LogInformation("CreateTopic. AuthorId: {AuthorId}", authorId);

        if (await MuteGuard.CheckAsync(_context, authorId) is { } muted)
            return Result<TopicDetailDto>.Fail(muted);

        try
        {
            var topic = new ForumTopic
            {
                Id = Guid.NewGuid(),
                Title = dto.Title.Trim(),
                Body = dto.Body.Trim(),
                AuthorId = authorId,
                CreatedAt = DateTime.UtcNow,
                Tags = NormalizeTags(dto.Tags),
            };
            _context.ForumTopics.Add(topic);
            await _context.SaveChangesAsync();

            // Author info for the response (a brand-new topic has no replies/views yet).
            var author = await _context.Users
                .Where(u => u.Id == authorId)
                .Select(u => new { u.Name, u.AvatarFileId })
                .FirstAsync();

            _logger.LogInformation("Topic created. TopicId: {TopicId}", topic.Id);
            return Result<TopicDetailDto>.Ok(new TopicDetailDto
            {
                Id = topic.Id,
                Title = topic.Title,
                Body = topic.Body,
                AuthorId = authorId,
                AuthorName = author.Name,
                AuthorAvatarUrl = UserMapper.AvatarUrl(authorId, author.AvatarFileId),
                ViewCount = 0,
                IsPinned = false,
                IsLocked = false,
                CreatedAt = topic.CreatedAt,
                Tags = topic.Tags,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating topic. AuthorId: {AuthorId}", authorId);
            return Result<TopicDetailDto>.Fail("An unexpected error occurred.");
        }
    }

    /// <inheritdoc />
    public async Task<Result<PagedResult<TopicListItemDto>>> GetTopicsAsync(
        Guid? authorId = null, int page = 1, int pageSize = 20,
        string? search = null, bool? solved = null, string? sort = null, string? tag = null)
    {
        // Clamp paging to sane bounds.
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        // Optional filter: only topics started by the given author.
        var query = _context.ForumTopics
            .Where(t => authorId == null || t.AuthorId == authorId);

        // Optional text search over title and body.
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = $"%{search.Trim()}%";
            query = query.Where(t => EF.Functions.ILike(t.Title, term) || EF.Functions.ILike(t.Body, term));
        }

        // Optional solved/unsolved filter (a topic is solved if any live reply is a solution).
        if (solved is bool wantSolved)
            query = query.Where(t => t.Replies.Any(r => r.IsSolution && !r.IsDeleted) == wantSolved);

        // Optional tag filter (exact match against the topic's tags).
        if (!string.IsNullOrWhiteSpace(tag))
        {
            var wanted = tag.Trim();
            query = query.Where(t => t.Tags.Contains(wanted));
        }

        var total = await query.CountAsync();

        // Pinned topics always come first; the rest follow the chosen sort.
        IOrderedQueryable<ForumTopic> ordered = sort switch
        {
            "active" => query.OrderByDescending(t => t.IsPinned)
                             .ThenByDescending(t => t.Replies.Where(r => !r.IsDeleted).Max(r => (DateTime?)r.CreatedAt) ?? t.CreatedAt),
            "top" => query.OrderByDescending(t => t.IsPinned).ThenByDescending(t => t.ViewCount),
            _ => query.OrderByDescending(t => t.IsPinned).ThenByDescending(t => t.CreatedAt),
        };

        var rows = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new
            {
                t.Id,
                t.Title,
                t.AuthorId,
                AuthorName = t.Author.Name,
                AuthorAvatarFileId = t.Author.AvatarFileId,
                t.ViewCount,
                ReplyCount = t.Replies.Count(r => !r.IsDeleted),
                t.IsPinned,
                t.IsLocked,
                IsSolved = t.Replies.Any(r => r.IsSolution && !r.IsDeleted),
                t.CreatedAt,
                LastActivityAt = t.Replies.Where(r => !r.IsDeleted).Max(r => (DateTime?)r.CreatedAt) ?? t.CreatedAt,
                t.Tags,
            })
            .AsNoTracking()
            .ToListAsync();

        // Compose the avatar URL in memory (EF can't translate the interpolation).
        var topics = rows
            .Select(t => new TopicListItemDto
            {
                Id = t.Id,
                Title = t.Title,
                AuthorId = t.AuthorId,
                AuthorName = t.AuthorName,
                AuthorAvatarUrl = UserMapper.AvatarUrl(t.AuthorId, t.AuthorAvatarFileId),
                ViewCount = t.ViewCount,
                ReplyCount = t.ReplyCount,
                IsPinned = t.IsPinned,
                IsLocked = t.IsLocked,
                IsSolved = t.IsSolved,
                CreatedAt = t.CreatedAt,
                LastActivityAt = t.LastActivityAt,
                Tags = t.Tags,
            })
            .ToList();

        return Result<PagedResult<TopicListItemDto>>.Ok(new PagedResult<TopicListItemDto>
        {
            Items = topics,
            Total = total,
            Page = page,
            PageSize = pageSize,
        });
    }

    /// <inheritdoc />
    public async Task<Result<TopicDetailDto>> GetTopicAsync(Guid topicId, Guid currentUserId)
    {
        var topic = await _context.ForumTopics
            .Include(t => t.Author)
            .Include(t => t.Replies).ThenInclude(r => r.Author)
            .Include(t => t.Replies).ThenInclude(r => r.Votes)
            .Include(t => t.Replies).ThenInclude(r => r.Reactions)
            .FirstOrDefaultAsync(t => t.Id == topicId);

        if (topic is null)
            return Result<TopicDetailDto>.Fail("Topic not found.");

        var isSubscribed = await _context.ForumTopicSubscriptions
            .AnyAsync(s => s.TopicId == topicId && s.UserId == currentUserId);

        // Views are counted separately via IncrementViewAsync (called once per page
        // open), so re-fetching a topic — after voting, replying, etc. — never inflates
        // the count.
        var dto = MapDetail(topic, currentUserId);
        dto.IsSubscribed = isSubscribed;
        return Result<TopicDetailDto>.Ok(dto);
    }

    /// <inheritdoc />
    public async Task<Result> IncrementViewAsync(Guid topicId)
    {
        var topic = await _context.ForumTopics.FirstOrDefaultAsync(t => t.Id == topicId);
        if (topic is null)
            return Result.Fail("Topic not found.");

        topic.ViewCount++;
        await _context.SaveChangesAsync();
        return Result.Ok();
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

        // Let the reply's author know their answer was accepted (unless it's their own topic).
        if (reply.AuthorId != userId)
        {
            await _notifications.CreateAsync(
                reply.AuthorId,
                NotificationType.Forum,
                $"Your reply was accepted as the solution in \"{reply.Topic.Title}\".",
                $"/forum/{reply.TopicId}");
        }

        // Credit the reply author's reputation ledger once for this accepted solution.
        await _reputation.RecordAsync(reply.AuthorId, 15, ReputationReason.ForumSolution, reply.Topic.Title, reply.Id, once: true);

        _logger.LogInformation("Solution marked. TopicId: {TopicId}, ReplyId: {ReplyId}", reply.TopicId, replyId);
        return Result.Ok();
    }

    /// <inheritdoc />
    public async Task<Result> DeleteTopicAsync(Guid topicId, Guid userId, bool isAdmin)
    {
        var topic = await _context.ForumTopics.FirstOrDefaultAsync(t => t.Id == topicId);
        if (topic is null)
            return Result.Fail("Topic not found.");

        // Only the author or an admin may delete a topic.
        if (topic.AuthorId != userId && !isAdmin)
            return Result.Fail("You can only delete your own topics.");

        // Remove attached files first. The link rows would cascade with the topic, but the
        // files themselves would be left orphaned in storage forever.
        await _attachments.DeleteAllForTopicAsync(topicId);

        // Removing the topic cascades to its replies and their votes (FK Cascade).
        _context.ForumTopics.Remove(topic);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Topic deleted. TopicId: {TopicId}, By: {UserId}, Admin: {IsAdmin}", topicId, userId, isAdmin);
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

        if (await MuteGuard.CheckAsync(_context, authorId) is { } muted)
            return Result<ReplyDto>.Fail(muted);

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

            var author = await _context.Users
                .Where(u => u.Id == authorId)
                .Select(u => new { u.Name, u.AvatarFileId })
                .FirstAsync();
            var authorName = author.Name;

            // Track who has already been notified so nobody gets a duplicate.
            var notified = new HashSet<Guid> { authorId };

            // @mentions take priority: notify anyone taking part in the topic who is
            // named in the reply (topic author + everyone who has replied).
            var replyParticipants = await _context.ForumReplies
                .Where(r => r.TopicId == topic.Id)
                .Select(r => new { r.AuthorId, Name = r.Author.Name })
                .Distinct()
                .ToListAsync();
            var topicAuthorName = await _context.Users
                .Where(u => u.Id == topic.AuthorId).Select(u => u.Name).FirstAsync();
            var audience = replyParticipants
                .Select(p => (Id: p.AuthorId, p.Name))
                .Append((Id: topic.AuthorId, Name: topicAuthorName))
                .Where(a => a.Id != authorId)
                .DistinctBy(a => a.Id)
                .ToList();
            foreach (var mentionedId in MentionParser.Extract(reply.Body, audience))
            {
                if (notified.Add(mentionedId))
                    await _notifications.CreateAsync(
                        mentionedId,
                        NotificationType.Forum,
                        $"{authorName} mentioned you in \"{topic.Title}\".",
                        $"/forum/{topic.Id}");
            }

            // Notify the topic author (unless already notified or replying to their own topic).
            if (notified.Add(topic.AuthorId))
            {
                await _notifications.CreateAsync(
                    topic.AuthorId,
                    NotificationType.Forum,
                    $"{authorName} replied to your topic \"{topic.Title}\".",
                    $"/forum/{topic.Id}");
            }

            // For a nested reply, also notify the parent reply's author (if not already notified).
            if (dto.ParentReplyId.HasValue)
            {
                var parentAuthorId = await _context.ForumReplies
                    .Where(r => r.Id == dto.ParentReplyId.Value)
                    .Select(r => (Guid?)r.AuthorId)
                    .FirstOrDefaultAsync();
                if (parentAuthorId is Guid pid && notified.Add(pid))
                {
                    await _notifications.CreateAsync(
                        pid,
                        NotificationType.Forum,
                        $"{authorName} replied to your comment in \"{topic.Title}\".",
                        $"/forum/{topic.Id}");
                }
            }

            // Notify everyone subscribed to the topic (skipping already-notified users).
            var subscriberIds = await _context.ForumTopicSubscriptions
                .Where(s => s.TopicId == topic.Id && !notified.Contains(s.UserId))
                .Select(s => s.UserId)
                .ToListAsync();
            foreach (var subscriberId in subscriberIds)
            {
                await _notifications.CreateAsync(
                    subscriberId,
                    NotificationType.Forum,
                    $"{authorName} replied to \"{topic.Title}\" you follow.",
                    $"/forum/{topic.Id}");
            }

            _logger.LogInformation("Reply added. ReplyId: {ReplyId}", reply.Id);
            // A brand-new reply has no votes yet.
            return Result<ReplyDto>.Ok(new ReplyDto
            {
                Id = reply.Id,
                TopicId = reply.TopicId,
                AuthorId = authorId,
                AuthorName = authorName,
                AuthorAvatarUrl = UserMapper.AvatarUrl(authorId, author.AvatarFileId),
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

    /// <inheritdoc />
    public async Task<Result<ReplyDto>> EditReplyAsync(Guid userId, Guid replyId, string body, bool isAdmin)
    {
        var reply = await _context.ForumReplies
            .Include(r => r.Author)
            .Include(r => r.Votes)
            .Include(r => r.Reactions)
            .FirstOrDefaultAsync(r => r.Id == replyId);
        if (reply is null)
            return Result<ReplyDto>.Fail("Reply not found.");

        // Only the reply's author or an admin may edit it.
        if (reply.AuthorId != userId && !isAdmin)
            return Result<ReplyDto>.Fail("You can only edit your own replies.");

        reply.Body = body.Trim();
        reply.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Reply edited. ReplyId: {ReplyId}, By: {UserId}, Admin: {IsAdmin}", replyId, userId, isAdmin);
        return Result<ReplyDto>.Ok(MapReply(reply, userId));
    }

    /// <inheritdoc />
    public async Task<Result> DeleteReplyAsync(Guid userId, Guid replyId, bool isAdmin)
    {
        var reply = await _context.ForumReplies.FirstOrDefaultAsync(r => r.Id == replyId);
        if (reply is null)
            return Result.Fail("Reply not found.");

        // Only the reply's author or an admin may delete it.
        if (reply.AuthorId != userId && !isAdmin)
            return Result.Fail("You can only delete your own replies.");

        // Soft-delete: keep the row so any child replies remain threaded, and
        // clear the accepted-solution flag if this reply held it.
        reply.IsDeleted = true;
        reply.IsSolution = false;
        reply.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Reply deleted. ReplyId: {ReplyId}, By: {UserId}, Admin: {IsAdmin}", replyId, userId, isAdmin);
        return Result.Ok();
    }

    /// <inheritdoc />
    public async Task<Result<List<ReactionDto>>> ToggleReplyReactionAsync(Guid userId, Guid replyId, string emoji)
    {
        emoji = emoji.Trim();
        if (string.IsNullOrEmpty(emoji) || emoji.Length > 16)
            return Result<List<ReactionDto>>.Fail("Invalid emoji.");

        var reply = await _context.ForumReplies.FirstOrDefaultAsync(r => r.Id == replyId);
        if (reply is null || reply.IsDeleted)
            return Result<List<ReactionDto>>.Fail("Reply not found.");

        var existing = await _context.ForumReplyReactions
            .FirstOrDefaultAsync(r => r.ReplyId == replyId && r.UserId == userId && r.Emoji == emoji);
        if (existing is null)
            _context.ForumReplyReactions.Add(new ForumReplyReaction
            {
                Id = Guid.NewGuid(),
                ReplyId = replyId,
                UserId = userId,
                Emoji = emoji,
                CreatedAt = DateTime.UtcNow,
            });
        else
            _context.ForumReplyReactions.Remove(existing);

        await _context.SaveChangesAsync();

        var reactions = await _context.ForumReplyReactions
            .Where(r => r.ReplyId == replyId)
            .AsNoTracking()
            .ToListAsync();

        return Result<List<ReactionDto>>.Ok(MapReactions(reactions, userId));
    }

    /// <inheritdoc />
    public async Task<Result> SetTopicPinnedAsync(Guid topicId, Guid userId, bool pinned, bool isAdmin)
    {
        if (!isAdmin)
            return Result.Fail("Only an admin can pin topics.");

        var topic = await _context.ForumTopics.FirstOrDefaultAsync(t => t.Id == topicId);
        if (topic is null)
            return Result.Fail("Topic not found.");

        topic.IsPinned = pinned;
        await _context.SaveChangesAsync();
        _logger.LogInformation("Topic pin set. TopicId: {TopicId}, Pinned: {Pinned}, By: {UserId}", topicId, pinned, userId);
        return Result.Ok();
    }

    /// <inheritdoc />
    public async Task<Result> SetTopicLockedAsync(Guid topicId, Guid userId, bool locked, bool isAdmin)
    {
        var topic = await _context.ForumTopics.FirstOrDefaultAsync(t => t.Id == topicId);
        if (topic is null)
            return Result.Fail("Topic not found.");

        if (topic.AuthorId != userId && !isAdmin)
            return Result.Fail("You can only lock your own topics.");

        topic.IsLocked = locked;
        await _context.SaveChangesAsync();
        _logger.LogInformation("Topic lock set. TopicId: {TopicId}, Locked: {Locked}, By: {UserId}", topicId, locked, userId);
        return Result.Ok();
    }

    /// <inheritdoc />
    public async Task<Result<bool>> ToggleSubscriptionAsync(Guid topicId, Guid userId)
    {
        var exists = await _context.ForumTopics.AnyAsync(t => t.Id == topicId);
        if (!exists)
            return Result<bool>.Fail("Topic not found.");

        var existing = await _context.ForumTopicSubscriptions
            .FirstOrDefaultAsync(s => s.TopicId == topicId && s.UserId == userId);
        if (existing is null)
        {
            _context.ForumTopicSubscriptions.Add(new ForumTopicSubscription
            {
                Id = Guid.NewGuid(),
                TopicId = topicId,
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
            });
            await _context.SaveChangesAsync();
            return Result<bool>.Ok(true);
        }

        _context.ForumTopicSubscriptions.Remove(existing);
        await _context.SaveChangesAsync();
        return Result<bool>.Ok(false);
    }

    /// <inheritdoc />
    public async Task<Result> ReportReplyAsync(Guid reporterId, Guid replyId, string? reason)
    {
        var reply = await _context.ForumReplies.FirstOrDefaultAsync(r => r.Id == replyId);
        if (reply is null || reply.IsDeleted)
            return Result.Fail("Reply not found.");

        // Don't stack duplicate pending reports from the same user on the same reply.
        var alreadyPending = await _context.ForumReports.AnyAsync(r =>
            r.ReplyId == replyId && r.ReporterId == reporterId && r.Status == ForumReportStatus.Pending);
        if (alreadyPending)
            return Result.Ok();

        _context.ForumReports.Add(new ForumReport
        {
            Id = Guid.NewGuid(),
            ReplyId = replyId,
            ReporterId = reporterId,
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            Status = ForumReportStatus.Pending,
            CreatedAt = DateTime.UtcNow,
        });
        await _context.SaveChangesAsync();
        _logger.LogInformation("Reply reported. ReplyId: {ReplyId}, By: {ReporterId}", replyId, reporterId);
        return Result.Ok();
    }

    /// <inheritdoc />
    public async Task<Result<List<ForumReportDto>>> GetReportsAsync(string? status = null)
    {
        var query = _context.ForumReports.AsQueryable();
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ForumReportStatus>(status, true, out var parsed))
            query = query.Where(r => r.Status == parsed);

        var reports = await query
            .OrderBy(r => r.Status == ForumReportStatus.Pending ? 0 : 1)
            .ThenByDescending(r => r.CreatedAt)
            .Select(r => new ForumReportDto
            {
                Id = r.Id,
                ReplyId = r.ReplyId,
                TopicId = r.Reply.TopicId,
                TopicTitle = r.Reply.Topic.Title,
                ReplyExcerpt = r.Reply.Body.Length > 200 ? r.Reply.Body.Substring(0, 200) : r.Reply.Body,
                ReplyAuthorName = r.Reply.Author.Name,
                ReporterId = r.ReporterId,
                ReporterName = r.Reporter.Name,
                Reason = r.Reason,
                Status = r.Status.ToString(),
                CreatedAt = r.CreatedAt,
            })
            .AsNoTracking()
            .ToListAsync();

        return Result<List<ForumReportDto>>.Ok(reports);
    }

    /// <inheritdoc />
    public async Task<Result> ResolveReportAsync(Guid adminId, Guid reportId, bool dismiss)
    {
        var report = await _context.ForumReports.FirstOrDefaultAsync(r => r.Id == reportId);
        if (report is null)
            return Result.Fail("Report not found.");

        report.Status = dismiss ? ForumReportStatus.Dismissed : ForumReportStatus.Resolved;
        report.ResolvedById = adminId;
        report.ResolvedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        _logger.LogInformation("Report resolved. ReportId: {ReportId}, Dismiss: {Dismiss}, By: {AdminId}", reportId, dismiss, adminId);
        return Result.Ok();
    }

    /// <inheritdoc />
    public Task<int> GetPendingReportCountAsync() =>
        _context.ForumReports.CountAsync(r => r.Status == ForumReportStatus.Pending);

    /// <inheritdoc />
    public async Task<Result<TopicDetailDto>> EditTopicAsync(Guid userId, Guid topicId, string title, string body, List<string> tags, bool isAdmin)
    {
        var topic = await _context.ForumTopics
            .Include(t => t.Author)
            .Include(t => t.Replies).ThenInclude(r => r.Author)
            .Include(t => t.Replies).ThenInclude(r => r.Votes)
            .Include(t => t.Replies).ThenInclude(r => r.Reactions)
            .FirstOrDefaultAsync(t => t.Id == topicId);
        if (topic is null)
            return Result<TopicDetailDto>.Fail("Topic not found.");

        // Only the topic's author or an admin may edit it.
        if (topic.AuthorId != userId && !isAdmin)
            return Result<TopicDetailDto>.Fail("You can only edit your own topics.");

        topic.Title = title.Trim();
        topic.Body = body.Trim();
        topic.Tags = NormalizeTags(tags);
        topic.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Topic edited. TopicId: {TopicId}, By: {UserId}, Admin: {IsAdmin}", topicId, userId, isAdmin);
        return Result<TopicDetailDto>.Ok(MapDetail(topic, userId));
    }

    // --- helpers ---

    /// <summary>
    /// Cleans up incoming tags: trims, drops blanks, caps length at 30 and count at 5,
    /// and de-duplicates case-insensitively (keeping the first spelling).
    /// </summary>
    private static List<string> NormalizeTags(IEnumerable<string>? tags)
    {
        if (tags is null) return new List<string>();
        var result = new List<string>();
        foreach (var raw in tags)
        {
            var tag = raw?.Trim();
            if (string.IsNullOrEmpty(tag)) continue;
            if (tag.Length > 30) tag = tag.Substring(0, 30);
            if (result.Any(x => string.Equals(x, tag, StringComparison.OrdinalIgnoreCase))) continue;
            result.Add(tag);
            if (result.Count >= 5) break;
        }
        return result;
    }

    // --- mapping ---

    private static TopicDetailDto MapDetail(ForumTopic t, Guid currentUserId) => new()
    {
        Id = t.Id,
        Title = t.Title,
        Body = t.Body,
        AuthorId = t.AuthorId,
        AuthorName = t.Author?.Name ?? string.Empty,
        AuthorAvatarUrl = t.Author is null ? null : UserMapper.AvatarUrl(t.Author),
        ViewCount = t.ViewCount,
        IsPinned = t.IsPinned,
        IsLocked = t.IsLocked,
        CreatedAt = t.CreatedAt,
        UpdatedAt = t.UpdatedAt,
        Tags = t.Tags,
        // Deleted replies are hidden entirely (their row is kept only so any
        // child replies that referenced them stay valid under the FK).
        Replies = t.Replies
            .Where(r => !r.IsDeleted)
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
        AuthorAvatarUrl = r.Author is null ? null : UserMapper.AvatarUrl(r.Author),
        // Deleted replies keep their row (to preserve threading) but hide the text.
        Body = r.IsDeleted ? string.Empty : r.Body,
        ParentReplyId = r.ParentReplyId,
        IsSolution = r.IsSolution,
        IsDeleted = r.IsDeleted,
        Score = r.Votes?.Sum(v => v.Value) ?? 0,
        MyVote = r.Votes?.FirstOrDefault(v => v.UserId == currentUserId)?.Value ?? 0,
        CreatedAt = r.CreatedAt,
        UpdatedAt = r.UpdatedAt,
        Reactions = MapReactions(r.Reactions, currentUserId),
    };

    /// <summary>Groups a reply's reactions by emoji and flags the current user's own.</summary>
    private static List<ReactionDto> MapReactions(IEnumerable<ForumReplyReaction>? reactions, Guid currentUserId) =>
        (reactions ?? Enumerable.Empty<ForumReplyReaction>())
            .GroupBy(r => r.Emoji)
            .OrderBy(g => g.Min(r => r.CreatedAt))
            .Select(g => new ReactionDto
            {
                Emoji = g.Key,
                Count = g.Count(),
                Mine = g.Any(r => r.UserId == currentUserId),
            })
            .ToList();
}
