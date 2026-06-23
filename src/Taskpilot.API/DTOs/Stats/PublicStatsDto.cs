namespace Taskpilot.API.DTOs.Stats;

/// <summary>
/// Public, safe-to-show site statistics (forum-style footer). Excludes the
/// anonymous-visitor analytics that are admin-only.
/// </summary>
public class PublicStatsDto
{
    /// <summary>Total registered users.</summary>
    public int TotalUsers { get; set; }

    /// <summary>Name of the most recently registered user (or null if none).</summary>
    public string? NewestUserName { get; set; }

    /// <summary>Total forum topics.</summary>
    public int TotalTopics { get; set; }

    /// <summary>Total forum replies (posts).</summary>
    public int TotalForumPosts { get; set; }

    /// <summary>Number of registered users currently online.</summary>
    public int OnlineUsers { get; set; }

    /// <summary>Names of the registered users currently online.</summary>
    public List<string> OnlineUserNames { get; set; } = new();
}
