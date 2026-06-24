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

    /// <summary>Returns the messages of a conversation the user belongs to (oldest first).</summary>
    Task<Result<List<MessageDto>>> GetMessagesAsync(Guid conversationId, Guid userId);

    /// <summary>Posts a new message to a conversation the user belongs to.</summary>
    Task<Result<MessageDto>> SendMessageAsync(Guid senderId, SendMessageDto dto);

    /// <summary>Deletes a message. Only its sender may delete it.</summary>
    Task<Result> DeleteMessageAsync(Guid messageId, Guid userId);

    /// <summary>Checks whether a user is a participant of a conversation.</summary>
    Task<bool> IsParticipantAsync(Guid conversationId, Guid userId);
}
