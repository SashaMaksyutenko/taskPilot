using System.Collections.Concurrent;

namespace Taskpilot.API.Hubs;

/// <summary>
/// Tracks which users are currently connected to the chat hub.
/// A user may have several connections (multiple tabs/devices), so we keep a set
/// of connection ids per user and consider them online while any connection remains.
/// Registered as a singleton so the state is shared across the app.
/// </summary>
public class PresenceTracker
{
    private readonly ConcurrentDictionary<Guid, HashSet<string>> _connections = new();

    /// <summary>Records a new connection for a user.</summary>
    public void Connected(Guid userId, string connectionId)
    {
        var set = _connections.GetOrAdd(userId, _ => new HashSet<string>());
        lock (set)
        {
            set.Add(connectionId);
        }
    }

    /// <summary>Removes a connection; drops the user once they have no connections left.</summary>
    public void Disconnected(Guid userId, string connectionId)
    {
        if (!_connections.TryGetValue(userId, out var set))
            return;

        lock (set)
        {
            set.Remove(connectionId);
            if (set.Count == 0)
                _connections.TryRemove(userId, out _);
        }
    }

    /// <summary>True if the user has at least one active connection.</summary>
    public bool IsOnline(Guid userId) => _connections.ContainsKey(userId);
}
