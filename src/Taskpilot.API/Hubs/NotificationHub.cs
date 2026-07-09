using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Taskpilot.API.Hubs;

/// <summary>
/// SignalR hub for real-time in-app notifications. Each connection joins a group
/// named after its user, so the server can push a notification to one specific
/// person regardless of which/how many devices they have open.
///
/// Receive event: "ReceiveNotification" with a NotificationDto payload.
/// </summary>
[Authorize]
public class NotificationHub : Hub
{
    private readonly PresenceTracker _presence;

    public NotificationHub(PresenceTracker presence)
    {
        _presence = presence;
    }

    /// <summary>Adds the connecting user to their personal group and marks them online.</summary>
    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        if (userId is not null)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(userId.Value));
            // The notification hub is connected on every page, so tracking presence
            // here (not only in ChatHub) reflects anyone with the app open.
            _presence.Connected(userId.Value, Context.ConnectionId);
        }
        await base.OnConnectedAsync();
    }

    /// <summary>Marks the user offline once this connection drops.</summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        if (userId is not null)
            _presence.Disconnected(userId.Value, Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>SignalR group name for a user's personal notification stream.</summary>
    public static string GroupName(Guid userId) => $"user-{userId}";

    /// <summary>Current user's id from the JWT "sub" claim, or null.</summary>
    private Guid? GetUserId()
    {
        var sub = Context.User?.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}
