using Microsoft.EntityFrameworkCore;
using Taskpilot.API.Common;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Chat;
using Taskpilot.API.Models;

namespace Taskpilot.API.Services;

/// <summary>
/// Handles chat business logic: creating conversations, listing them,
/// reading and posting messages. Access is checked so a user can only touch
/// conversations they participate in.
/// </summary>
public class ChatService : IChatService
{
    private readonly TaskpilotDbContext _context;
    private readonly INotificationService _notifications;
    private readonly ILogger<ChatService> _logger;

    public ChatService(
        TaskpilotDbContext context,
        INotificationService notifications,
        ILogger<ChatService> logger)
    {
        _context = context;
        _notifications = notifications;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<ConversationDto>> StartDirectConversationAsync(Guid userId, Guid otherUserId)
    {
        _logger.LogInformation("StartDirectConversation. UserId: {UserId}, OtherUserId: {OtherUserId}", userId, otherUserId);

        if (otherUserId == userId)
            return Result<ConversationDto>.Fail("Cannot start a conversation with yourself.");

        var otherExists = await _context.Users.AnyAsync(u => u.Id == otherUserId);
        if (!otherExists)
            return Result<ConversationDto>.Fail("The other user does not exist.");

        try
        {
            // Look for an existing direct conversation that contains both users.
            var existing = await _context.Conversations
                .Where(c => c.Type == ConversationType.Direct
                            && c.Participants.Any(p => p.UserId == userId)
                            && c.Participants.Any(p => p.UserId == otherUserId))
                .Include(c => c.Participants).ThenInclude(p => p.User)
                .FirstOrDefaultAsync();

            if (existing is not null)
                return Result<ConversationDto>.Ok(MapConversation(existing));

            // Create a new direct conversation with the two participants.
            var conversation = new Conversation
            {
                Id = Guid.NewGuid(),
                Type = ConversationType.Direct,
                CreatedAt = DateTime.UtcNow,
            };
            conversation.Participants.Add(NewParticipant(conversation.Id, userId));
            conversation.Participants.Add(NewParticipant(conversation.Id, otherUserId));

            _context.Conversations.Add(conversation);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Direct conversation created. ConversationId: {ConversationId}", conversation.Id);
            return Result<ConversationDto>.Ok(await LoadConversationDtoAsync(conversation.Id));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting direct conversation. UserId: {UserId}", userId);
            return Result<ConversationDto>.Fail("An unexpected error occurred.");
        }
    }

    /// <inheritdoc />
    public async Task<Result<ConversationDto>> CreateGroupConversationAsync(Guid creatorId, CreateGroupConversationDto dto)
    {
        _logger.LogInformation("CreateGroupConversation. CreatorId: {CreatorId}", creatorId);

        try
        {
            // Distinct ids that exist and are not the creator.
            var requestedIds = dto.ParticipantIds.Distinct().Where(id => id != creatorId).ToList();
            var validIds = await _context.Users
                .Where(u => requestedIds.Contains(u.Id))
                .Select(u => u.Id)
                .ToListAsync();

            var conversation = new Conversation
            {
                Id = Guid.NewGuid(),
                Type = ConversationType.Group,
                Name = dto.Name.Trim(),
                CreatedAt = DateTime.UtcNow,
            };
            conversation.Participants.Add(NewParticipant(conversation.Id, creatorId));
            foreach (var id in validIds)
                conversation.Participants.Add(NewParticipant(conversation.Id, id));

            _context.Conversations.Add(conversation);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Group conversation created. ConversationId: {ConversationId}, Members: {Count}",
                conversation.Id, conversation.Participants.Count);
            return Result<ConversationDto>.Ok(await LoadConversationDtoAsync(conversation.Id));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating group conversation. CreatorId: {CreatorId}", creatorId);
            return Result<ConversationDto>.Fail("An unexpected error occurred.");
        }
    }

    /// <inheritdoc />
    public async Task<Result<List<ConversationDto>>> GetUserConversationsAsync(Guid userId)
    {
        _logger.LogInformation("GetUserConversations. UserId: {UserId}", userId);

        var conversations = await _context.Conversations
            .Where(c => c.Participants.Any(p => p.UserId == userId))
            .Include(c => c.Participants).ThenInclude(p => p.User)
            .OrderByDescending(c => c.CreatedAt)
            .AsNoTracking()
            .ToListAsync();

        return Result<List<ConversationDto>>.Ok(conversations.Select(MapConversation).ToList());
    }

    /// <inheritdoc />
    public async Task<Result<List<MessageDto>>> GetMessagesAsync(Guid conversationId, Guid userId)
    {
        _logger.LogInformation("GetMessages. ConversationId: {ConversationId}, UserId: {UserId}", conversationId, userId);

        if (!await IsParticipantAsync(conversationId, userId))
            return Result<List<MessageDto>>.Fail("You are not a participant of this conversation.");

        var messages = await _context.Messages
            .Where(m => m.ConversationId == conversationId)
            .OrderBy(m => m.CreatedAt)
            .Include(m => m.Sender)
            .AsNoTracking()
            .ToListAsync();

        return Result<List<MessageDto>>.Ok(messages.Select(MapMessage).ToList());
    }

    /// <inheritdoc />
    public async Task<Result<MessageDto>> SendMessageAsync(Guid senderId, SendMessageDto dto)
    {
        _logger.LogInformation("SendMessage. ConversationId: {ConversationId}, SenderId: {SenderId}", dto.ConversationId, senderId);

        if (!await IsParticipantAsync(dto.ConversationId, senderId))
            return Result<MessageDto>.Fail("You are not a participant of this conversation.");

        try
        {
            var message = new Message
            {
                Id = Guid.NewGuid(),
                ConversationId = dto.ConversationId,
                SenderId = senderId,
                Content = dto.Content.Trim(),
                CreatedAt = DateTime.UtcNow,
            };
            _context.Messages.Add(message);
            await _context.SaveChangesAsync();

            // Sender name for the response.
            var senderName = await _context.Users
                .Where(u => u.Id == senderId)
                .Select(u => u.Name)
                .FirstAsync();

            // Notify the other participants of the conversation.
            var otherParticipantIds = await _context.ConversationParticipants
                .Where(p => p.ConversationId == dto.ConversationId && p.UserId != senderId)
                .Select(p => p.UserId)
                .ToListAsync();
            foreach (var participantId in otherParticipantIds)
            {
                await _notifications.CreateAsync(
                    participantId,
                    NotificationType.Chat,
                    $"New message from {senderName}",
                    "/chat");
            }

            _logger.LogInformation("Message sent. MessageId: {MessageId}", message.Id);
            return Result<MessageDto>.Ok(new MessageDto
            {
                Id = message.Id,
                ConversationId = message.ConversationId,
                SenderId = message.SenderId,
                SenderName = senderName,
                Content = message.Content,
                CreatedAt = message.CreatedAt,
                EditedAt = message.EditedAt,
                IsDeleted = message.IsDeleted,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message. ConversationId: {ConversationId}", dto.ConversationId);
            return Result<MessageDto>.Fail("An unexpected error occurred.");
        }
    }

    /// <inheritdoc />
    public Task<bool> IsParticipantAsync(Guid conversationId, Guid userId) =>
        _context.ConversationParticipants
            .AnyAsync(p => p.ConversationId == conversationId && p.UserId == userId);

    // --- helpers ---

    /// <summary>Builds a new participant entity.</summary>
    private static ConversationParticipant NewParticipant(Guid conversationId, Guid userId) => new()
    {
        Id = Guid.NewGuid(),
        ConversationId = conversationId,
        UserId = userId,
        JoinedAt = DateTime.UtcNow,
    };

    /// <summary>Loads a conversation (with participants) and maps it to a DTO.</summary>
    private async Task<ConversationDto> LoadConversationDtoAsync(Guid conversationId)
    {
        var conversation = await _context.Conversations
            .Include(c => c.Participants).ThenInclude(p => p.User)
            .AsNoTracking()
            .FirstAsync(c => c.Id == conversationId);
        return MapConversation(conversation);
    }

    private static ConversationDto MapConversation(Conversation c) => new()
    {
        Id = c.Id,
        Type = c.Type.ToString(),
        Name = c.Name,
        CreatedAt = c.CreatedAt,
        Participants = c.Participants
            .Select(p => new ParticipantDto { UserId = p.UserId, Name = p.User?.Name ?? string.Empty })
            .ToList(),
    };

    private static MessageDto MapMessage(Message m) => new()
    {
        Id = m.Id,
        ConversationId = m.ConversationId,
        SenderId = m.SenderId,
        SenderName = m.Sender?.Name ?? string.Empty,
        Content = m.Content,
        CreatedAt = m.CreatedAt,
        EditedAt = m.EditedAt,
        IsDeleted = m.IsDeleted,
    };
}
