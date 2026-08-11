using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Taskpilot.API.Data;
using Taskpilot.API.Services;

namespace Taskpilot.API.Hubs;

/// <summary>
/// Realtime channel for a project's whiteboard. Note CRUD is authoritative over REST
/// (WhiteboardController) so per-note permissions are enforced; this hub only carries the ephemeral
/// bits — live cursors and in-flight drag positions — and relays the server's create/update/delete
/// broadcasts to a project's board group.
///
/// Server → client: "NoteUpserted" (WhiteboardNoteDto), "NoteDeleted" (Guid), "Cursor", "LiveMove",
/// "PeerLeft" (connectionId).
/// </summary>
[Authorize]
public class WhiteboardHub : Hub
{
    private const string BoardKey = "whiteboard:project";

    private readonly TaskpilotDbContext _context;

    public WhiteboardHub(TaskpilotDbContext context)
    {
        _context = context;
    }

    /// <summary>Group carrying a project's whiteboard events.</summary>
    public static string GroupName(Guid projectId) => $"board-{projectId}";

    /// <summary>Subscribes to a project's whiteboard, if the caller can access the project.</summary>
    public async Task JoinBoard(Guid projectId)
    {
        var userId = GetUserId();
        if (userId is null || !await ProjectAccess.CanAccessAsync(_context, projectId, userId.Value))
            return;

        Context.Items[BoardKey] = projectId;
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(projectId));
    }

    /// <summary>Unsubscribes and tells peers to drop this connection's cursor.</summary>
    public async Task LeaveBoard(Guid projectId)
    {
        Context.Items.Remove(BoardKey);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(projectId));
        await Clients.OthersInGroup(GroupName(projectId)).SendAsync("PeerLeft", Context.ConnectionId);
    }

    /// <summary>Relays the caller's cursor position to the other editors (ephemeral).</summary>
    public Task SendCursor(Guid projectId, string name, string color, double x, double y)
    {
        if (!IsJoined(projectId)) return Task.CompletedTask;
        return Clients.OthersInGroup(GroupName(projectId))
            .SendAsync("Cursor", new { connectionId = Context.ConnectionId, name, color, x, y });
    }

    /// <summary>Relays an in-flight drag position so a note moves smoothly for others; the final
    /// position is persisted via REST on drop.</summary>
    public Task SendMove(Guid projectId, Guid noteId, double x, double y)
    {
        if (!IsJoined(projectId)) return Task.CompletedTask;
        return Clients.OthersInGroup(GroupName(projectId)).SendAsync("LiveMove", new { noteId, x, y });
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.Items.TryGetValue(BoardKey, out var value) && value is Guid projectId)
            await Clients.OthersInGroup(GroupName(projectId)).SendAsync("PeerLeft", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    private bool IsJoined(Guid projectId) =>
        Context.Items.TryGetValue(BoardKey, out var value) && value is Guid joined && joined == projectId;

    private Guid? GetUserId()
    {
        var sub = Context.User?.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}
