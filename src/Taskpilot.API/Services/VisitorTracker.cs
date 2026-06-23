namespace Taskpilot.API.Services;

/// <summary>
/// In-memory tracker of anonymous (not-logged-in) site visitors. Counts total
/// anonymous requests and the number of distinct visitor IPs seen "today" (UTC).
/// Registered as a singleton so the counts are shared across the app. The daily
/// set resets automatically when the UTC date changes.
/// </summary>
public class VisitorTracker
{
    private readonly object _lock = new();
    private HashSet<string> _todayIps = new();
    private DateOnly _day = DateOnly.FromDateTime(DateTime.UtcNow);
    private long _totalVisits;

    /// <summary>Records one anonymous request from the given IP (ip may be null).</summary>
    public void Record(string? ip)
    {
        lock (_lock)
        {
            RollOverIfNewDay();
            _totalVisits++;
            if (!string.IsNullOrEmpty(ip))
                _todayIps.Add(ip);
        }
    }

    /// <summary>Total anonymous requests counted since the app started.</summary>
    public long TotalVisits
    {
        get { lock (_lock) { return _totalVisits; } }
    }

    /// <summary>Number of distinct anonymous visitor IPs seen today (UTC).</summary>
    public int UniqueVisitorsToday
    {
        get { lock (_lock) { RollOverIfNewDay(); return _todayIps.Count; } }
    }

    // Clears the per-day set when the UTC date rolls over. Caller must hold the lock.
    private void RollOverIfNewDay()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (today != _day)
        {
            _day = today;
            _todayIps = new HashSet<string>();
        }
    }
}
