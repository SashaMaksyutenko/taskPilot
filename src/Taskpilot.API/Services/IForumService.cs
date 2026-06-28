using Taskpilot.API.Common;
using Taskpilot.API.DTOs.Common;
using Taskpilot.API.DTOs.Forum;

namespace Taskpilot.API.Services;

/// <summary>
/// Business logic for the forum: topics and replies.
/// </summary>
public interface IForumService
{
    /// <summary>Creates a new topic authored by the given user.</summary>
    Task<Result<TopicDetailDto>> CreateTopicAsync(Guid authorId, CreateTopicDto dto);

    /// <summary>
    /// Lists a page of topics (pinned first, then newest), optionally filtered by author.
    /// </summary>
    Task<Result<PagedResult<TopicListItemDto>>> GetTopicsAsync(Guid? authorId = null, int page = 1, int pageSize = 20);

    /// <summary>
    /// Returns a topic with its replies and increments its view count.
    /// <paramref name="currentUserId"/> is used to report the caller's own vote per reply.
    /// </summary>
    Task<Result<TopicDetailDto>> GetTopicAsync(Guid topicId, Guid currentUserId);

    /// <summary>Adds a reply to a topic (unless it is locked).</summary>
    Task<Result<ReplyDto>> AddReplyAsync(Guid authorId, CreateReplyDto dto);

    /// <summary>
    /// Deletes a topic (and its replies). Allowed only for the topic's author or an admin.
    /// </summary>
    Task<Result> DeleteTopicAsync(Guid topicId, Guid userId, bool isAdmin);

    /// <summary>
    /// Casts (or toggles/changes) a user's vote on a reply.
    /// Voting the same value again removes the vote. Returns the new score.
    /// </summary>
    Task<Result<VoteResultDto>> VoteReplyAsync(Guid userId, Guid replyId, int value);

    /// <summary>
    /// Marks a reply as the accepted solution. Only the topic author may do this;
    /// any previously marked solution in the same topic is cleared.
    /// </summary>
    Task<Result> MarkSolutionAsync(Guid userId, Guid replyId);
}
