using Taskpilot.API.Common;
using Taskpilot.API.DTOs.Chat;
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
    /// Lists a page of topics (pinned first, then by the chosen sort), optionally
    /// filtered by author, a title/body search term and solved/unsolved status.
    /// </summary>
    /// <param name="sort">"latest" (default), "active" (last reply) or "top" (most viewed).</param>
    Task<Result<PagedResult<TopicListItemDto>>> GetTopicsAsync(
        Guid? authorId = null, int page = 1, int pageSize = 20,
        string? search = null, bool? solved = null, string? sort = null);

    /// <summary>
    /// Returns a topic with its replies and increments its view count.
    /// <paramref name="currentUserId"/> is used to report the caller's own vote per reply.
    /// </summary>
    Task<Result<TopicDetailDto>> GetTopicAsync(Guid topicId, Guid currentUserId);

    /// <summary>Increments a topic's view count. Called once per page open (not on re-fetch).</summary>
    Task<Result> IncrementViewAsync(Guid topicId);

    /// <summary>Adds a reply to a topic (unless it is locked).</summary>
    Task<Result<ReplyDto>> AddReplyAsync(Guid authorId, CreateReplyDto dto);

    /// <summary>
    /// Edits a reply's body. Allowed only for the reply's author or an admin.
    /// Stamps <c>UpdatedAt</c> and returns the updated reply.
    /// </summary>
    Task<Result<ReplyDto>> EditReplyAsync(Guid userId, Guid replyId, string body, bool isAdmin);

    /// <summary>
    /// Soft-deletes a reply (author or admin only), preserving threading of child replies.
    /// </summary>
    Task<Result> DeleteReplyAsync(Guid userId, Guid replyId, bool isAdmin);

    /// <summary>
    /// Toggles the current user's emoji reaction on a reply. Returns the updated
    /// reaction summary for that reply.
    /// </summary>
    Task<Result<List<ReactionDto>>> ToggleReplyReactionAsync(Guid userId, Guid replyId, string emoji);

    /// <summary>Pins or unpins a topic. Admin only.</summary>
    Task<Result> SetTopicPinnedAsync(Guid topicId, Guid userId, bool pinned, bool isAdmin);

    /// <summary>Locks or unlocks a topic. Allowed for an admin or the topic's author.</summary>
    Task<Result> SetTopicLockedAsync(Guid topicId, Guid userId, bool locked, bool isAdmin);

    /// <summary>
    /// Toggles the current user's subscription to a topic. Returns whether they are
    /// now subscribed.
    /// </summary>
    Task<Result<bool>> ToggleSubscriptionAsync(Guid topicId, Guid userId);

    /// <summary>
    /// Edits a topic's title and body. Allowed only for the topic's author or an admin.
    /// </summary>
    Task<Result<TopicDetailDto>> EditTopicAsync(Guid userId, Guid topicId, string title, string body, bool isAdmin);

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
