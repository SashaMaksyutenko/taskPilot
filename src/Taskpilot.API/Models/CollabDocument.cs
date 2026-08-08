namespace Taskpilot.API.Models;

/// <summary>
/// The persisted state of a collaboratively-edited document (a Yjs CRDT snapshot). The server
/// never interprets the bytes — it stores them so a client that (re)connects can seed its local
/// CRDT and converge with everyone else. Live edits are relayed over <c>CollabHub</c>; this is
/// only the durable baseline.
/// </summary>
public class CollabDocument
{
    /// <summary>Stable document key, e.g. <c>"task:{guid}"</c>. Also the SignalR group name.</summary>
    public string Id { get; set; } = null!;

    /// <summary>The Yjs-encoded document state (opaque binary produced by the client).</summary>
    public byte[] State { get; set; } = System.Array.Empty<byte>();

    /// <summary>When the snapshot was last written.</summary>
    public DateTime UpdatedAt { get; set; }
}
