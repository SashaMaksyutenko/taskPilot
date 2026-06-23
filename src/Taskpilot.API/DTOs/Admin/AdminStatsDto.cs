namespace Taskpilot.API.DTOs.Admin;

/// <summary>Live site statistics for the admin dashboard.</summary>
public class AdminStatsDto
{
    /// <summary>Total registered users.</summary>
    public int TotalUsers { get; set; }

    /// <summary>Active (not banned) users.</summary>
    public int ActiveUsers { get; set; }

    /// <summary>Name of the most recently registered user (or null if none).</summary>
    public string? NewestUserName { get; set; }

    /// <summary>Total forum topics.</summary>
    public int TotalTopics { get; set; }

    /// <summary>Total forum replies (posts).</summary>
    public int TotalForumPosts { get; set; }

    /// <summary>Users currently connected in real time.</summary>
    public int OnlineUsers { get; set; }

    /// <summary>Names of the registered users currently online.</summary>
    public List<string> OnlineUserNames { get; set; } = new();

    /// <summary>Distinct anonymous (not logged-in) visitor IPs seen today (UTC).</summary>
    public int AnonymousVisitorsToday { get; set; }

    /// <summary>Total anonymous requests counted since the app started.</summary>
    public long AnonymousVisitsTotal { get; set; }
}
