namespace Taskpilot.API.Services;

/// <summary>
/// Access checks and durable-state storage for collaboratively-edited documents. The realtime
/// relay lives in <c>CollabHub</c>; this service owns the database side.
/// </summary>
public interface ICollabService
{
    /// <summary>
    /// True if <paramref name="userId"/> may co-edit the document identified by
    /// <paramref name="docId"/> (currently <c>"task:{guid}"</c> — write access to the task).
    /// Unknown or malformed ids return false.
    /// </summary>
    Task<bool> CanAccessAsync(string docId, Guid userId);

    /// <summary>The stored CRDT snapshot for a document, or null if none has been saved yet.</summary>
    Task<byte[]?> GetStateAsync(string docId);

    /// <summary>Upserts the CRDT snapshot for a document.</summary>
    Task SaveStateAsync(string docId, byte[] state);
}
