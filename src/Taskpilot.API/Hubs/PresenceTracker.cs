using System.Collections.Concurrent;

namespace Taskpilot.API.Hubs;

/// <summary>
/// Tracks which users are currently connected to the real-time hubs (chat and
/// notifications). A user may have several connections (multiple tabs/devices), so
/// we keep a set of connection ids per user and consider them online while any
/// connection remains.
///
/// A short grace period after the last connection drops keeps the user "online"
/// briefly, so a background-tab throttle or a quick auto-reconnect doesn't make the
/// online count flicker. Registered as a singleton so the state is shared app-wide.
/// </summary>
public class PresenceTracker
{
    private static readonly TimeSpan GracePeriod = TimeSpan.FromSeconds(15);

    private readonly ConcurrentDictionary<Guid, HashSet<string>> _connections = new();
    // Users with no live connection but still within their grace window → value is the UTC expiry.
    private readonly ConcurrentDictionary<Guid, DateTime> _graceUntil = new();

    /// <summary>Records a new connection for a user (and cancels any pending grace expiry).</summary>
    public void Connected(Guid userId, string connectionId)
    {
        var set = _connections.GetOrAdd(userId, _ => new HashSet<string>());
        lock (set)
        {
            set.Add(connectionId);
        }
        _graceUntil.TryRemove(userId, out _); // back online — no longer in grace
    }

    /// <summary>
    /// Removes a connection. When it was the user's last one, they stay online for a
    /// short grace period rather than dropping immediately.
    /// </summary>
    public void Disconnected(Guid userId, string connectionId)
    {
        if (!_connections.TryGetValue(userId, out var set))
            return;

        lock (set)
        {
            set.Remove(connectionId);
            if (set.Count == 0)
            {
                _connections.TryRemove(userId, out _);
                _graceUntil[userId] = DateTime.UtcNow.Add(GracePeriod);
            }
        }
    }

    /// <summary>True if the user has a live connection or is still within their grace window.</summary>
    public bool IsOnline(Guid userId) =>
        _connections.ContainsKey(userId) ||
        (_graceUntil.TryGetValue(userId, out var until) && until > DateTime.UtcNow);

    /// <summary>Number of distinct users currently online.</summary>
    public int OnlineCount => OnlineUserIds().Count;

    /// <summary>Ids of all users currently online (live connection or within grace).</summary>
    public IReadOnlyCollection<Guid> OnlineUserIds()
    {
        var now = DateTime.UtcNow;
        var ids = new HashSet<Guid>(_connections.Keys);
        foreach (var (userId, until) in _graceUntil)
        {
            if (until > now)
                ids.Add(userId);
            else
                _graceUntil.TryRemove(userId, out _); // purge expired grace entries
        }
        return ids.ToArray();
    }
}
