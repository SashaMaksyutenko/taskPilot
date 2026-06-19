using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Taskpilot.API.Services;

namespace Taskpilot.API.Hubs;

/// <summary>
/// SignalR hub for real-time chat. Clients connect here to receive messages
/// instantly. Sending is done over REST (ChatController); this hub manages
/// per-conversation groups and pushes new messages to their members.
///
/// Receive event: "ReceiveMessage" with a MessageDto payload.
/// </summary>
[Authorize]
public class ChatHub : Hub
{
    private readonly IChatService _chatService;
    private readonly PresenceTracker _presence;

    public ChatHub(IChatService chatService, PresenceTracker presence)
    {
        _chatService = chatService;
        _presence = presence;
    }

    /// <summary>Marks the connecting user as online.</summary>
    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        if (userId is not null)
            _presence.Connected(userId.Value, Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    /// <summary>Marks the user offline once their last connection drops.</summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        if (userId is not null)
            _presence.Disconnected(userId.Value, Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Subscribes the current connection to a conversation's message stream.
    /// Membership is verified so a user cannot listen to conversations they
    /// are not part of.
    /// </summary>
    public async Task JoinConversation(Guid conversationId)
    {
        var userId = GetUserId();
        if (userId is null)
            return;

        if (!await _chatService.IsParticipantAsync(conversationId, userId.Value))
            return;

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(conversationId));
    }

    /// <summary>Unsubscribes the current connection from a conversation.</summary>
    public Task LeaveConversation(Guid conversationId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(conversationId));

    /// <summary>SignalR group name for a conversation.</summary>
    public static string GroupName(Guid conversationId) => $"conversation-{conversationId}";

    /// <summary>Current user's id from the JWT "sub" claim, or null.</summary>
    private Guid? GetUserId()
    {
        var sub = Context.User?.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}
