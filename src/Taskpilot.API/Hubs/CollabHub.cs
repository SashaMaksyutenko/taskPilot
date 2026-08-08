using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Taskpilot.API.Services;

namespace Taskpilot.API.Hubs;

/// <summary>
/// SignalR hub for real-time collaborative editing (CRDT). The server is a relay + blob store:
/// it never interprets the Yjs payloads, it only forwards live updates and awareness (cursors)
/// between the editors of a document and persists periodic snapshots so late joiners can catch up.
/// Convergence is guaranteed by the CRDT itself, so relay order does not matter.
///
/// Client → server: JoinDocument, LeaveDocument, SendUpdate, SendAwareness, PersistState.
/// Server → client: ReceiveState (base64 snapshot on join), ReceiveUpdate, ReceiveAwareness,
/// PeerJoined (a new editor arrived — resend your awareness so they see your cursor).
/// All binary payloads cross the wire as base64 strings.
/// </summary>
[Authorize]
public class CollabHub : Hub
{
    // Per-connection set of document ids the caller has been authorized to edit. Access is
    // checked once on join (a DB hit) and cached here so the hot relay methods stay in-memory.
    private const string AuthorizedKey = "collab:docs";

    private readonly ICollabService _collab;

    public CollabHub(ICollabService collab)
    {
        _collab = collab;
    }

    /// <summary>
    /// Joins a document's edit session after an access check, streams back the stored snapshot,
    /// and asks existing editors to re-announce their cursors to the newcomer.
    /// </summary>
    public async Task JoinDocument(string docId)
    {
        var userId = GetUserId();
        if (userId is null || string.IsNullOrEmpty(docId))
            return;

        if (!await _collab.CanAccessAsync(docId, userId.Value))
            return;

        Authorized.Add(docId);
        await Groups.AddToGroupAsync(Context.ConnectionId, docId);

        var state = await _collab.GetStateAsync(docId);
        await Clients.Caller.SendAsync("ReceiveState", docId, state is null ? null : Convert.ToBase64String(state));

        // Existing editors resend their awareness so the newcomer sees their cursors right away.
        await Clients.OthersInGroup(docId).SendAsync("PeerJoined", docId);
    }

    /// <summary>Leaves a document's edit session.</summary>
    public async Task LeaveDocument(string docId)
    {
        Authorized.Remove(docId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, docId);
    }

    /// <summary>Relays a CRDT update to the other editors of the document.</summary>
    public Task SendUpdate(string docId, string update)
    {
        if (!Authorized.Contains(docId))
            return Task.CompletedTask;
        return Clients.OthersInGroup(docId).SendAsync("ReceiveUpdate", docId, update);
    }

    /// <summary>Relays an awareness (cursor/selection/presence) update. Ephemeral — never stored.</summary>
    public Task SendAwareness(string docId, string awareness)
    {
        if (!Authorized.Contains(docId))
            return Task.CompletedTask;
        return Clients.OthersInGroup(docId).SendAsync("ReceiveAwareness", docId, awareness);
    }

    /// <summary>Stores a full CRDT snapshot so future joiners start from the current text.</summary>
    public async Task PersistState(string docId, string state)
    {
        if (!Authorized.Contains(docId))
            return;
        await _collab.SaveStateAsync(docId, Convert.FromBase64String(state));
    }

    /// <summary>The document ids this connection is authorized to edit (per-connection state).</summary>
    private HashSet<string> Authorized
    {
        get
        {
            if (Context.Items.TryGetValue(AuthorizedKey, out var value) && value is HashSet<string> set)
                return set;
            var created = new HashSet<string>();
            Context.Items[AuthorizedKey] = created;
            return created;
        }
    }

    /// <summary>Current user's id from the JWT "sub" claim, or null.</summary>
    private Guid? GetUserId()
    {
        var sub = Context.User?.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}
