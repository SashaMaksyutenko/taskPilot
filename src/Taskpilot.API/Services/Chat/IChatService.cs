using Taskpilot.API.Common;
using Taskpilot.API.DTOs.Chat;

namespace Taskpilot.API.Services;

/// <summary>
/// Business logic for chat: conversations and messages.
/// </summary>
public interface IChatService
{
    /// <summary>
    /// Returns the existing direct (1:1) conversation between the two users,
    /// creating it if it does not exist yet.
    /// </summary>
    Task<Result<ConversationDto>> StartDirectConversationAsync(Guid userId, Guid otherUserId);

    /// <summary>Creates a group conversation; the creator is added automatically.</summary>
    Task<Result<ConversationDto>> CreateGroupConversationAsync(Guid creatorId, CreateGroupConversationDto dto);

    /// <summary>Lists all conversations the user takes part in.</summary>
    Task<Result<List<ConversationDto>>> GetUserConversationsAsync(Guid userId);

    /// <summary>Marks a conversation as read up to now for the given user; returns the read timestamp.</summary>
    Task<Result<DateTime>> MarkConversationReadAsync(Guid userId, Guid conversationId);

    /// <summary>Mutes or unmutes a conversation for the user; returns the new muted state.</summary>
    Task<Result<bool>> SetConversationMutedAsync(Guid userId, Guid conversationId, bool muted);

    /// <summary>Returns the ids of every conversation the user takes part in.</summary>
    Task<List<Guid>> GetConversationIdsAsync(Guid userId);

    /// <summary>Returns the messages of a conversation the user belongs to (oldest first).</summary>
    Task<Result<List<MessageDto>>> GetMessagesAsync(Guid conversationId, Guid userId);

    /// <summary>Posts a new message to a conversation the user belongs to.</summary>
    Task<Result<MessageDto>> SendMessageAsync(Guid senderId, SendMessageDto dto);

    /// <summary>Edits a message's text. Only its sender may edit it.</summary>
    Task<Result<MessageDto>> EditMessageAsync(Guid messageId, Guid userId, string content);

    /// <summary>Toggles the pinned state of a message; any participant may pin/unpin.</summary>
    Task<Result<MessageDto>> TogglePinAsync(Guid userId, Guid messageId);

    /// <summary>Deletes a message. Only its sender may delete it.</summary>
    Task<Result> DeleteMessageAsync(Guid messageId, Guid userId);

    /// <summary>Toggles an emoji reaction on a message; returns the updated reactions.</summary>
    Task<Result<ReactionUpdateDto>> ToggleReactionAsync(Guid userId, Guid messageId, string emoji);

    /// <summary>Checks whether a user is a participant of a conversation.</summary>
    Task<bool> IsParticipantAsync(Guid conversationId, Guid userId);
}
