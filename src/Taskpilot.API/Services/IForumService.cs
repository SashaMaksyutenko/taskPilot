using Taskpilot.API.Common;
using Taskpilot.API.DTOs.Forum;

namespace Taskpilot.API.Services;

/// <summary>
/// Business logic for the forum: topics and replies.
/// </summary>
public interface IForumService
{
    /// <summary>Creates a new topic authored by the given user.</summary>
    Task<Result<TopicDetailDto>> CreateTopicAsync(Guid authorId, CreateTopicDto dto);

    /// <summary>Lists all topics (pinned first, then newest).</summary>
    Task<Result<List<TopicListItemDto>>> GetTopicsAsync();

    /// <summary>Returns a topic with its replies and increments its view count.</summary>
    Task<Result<TopicDetailDto>> GetTopicAsync(Guid topicId);

    /// <summary>Adds a reply to a topic (unless it is locked).</summary>
    Task<Result<ReplyDto>> AddReplyAsync(Guid authorId, CreateReplyDto dto);
}
