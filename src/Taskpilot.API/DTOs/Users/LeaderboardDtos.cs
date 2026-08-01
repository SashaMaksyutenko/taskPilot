namespace Taskpilot.API.DTOs.Users;

/// <summary>One ranked user on the leaderboard.</summary>
public class LeaderboardEntryDto
{
    /// <summary>1-based position (ties are broken by tasks completed, then id).</summary>
    public int Rank { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }

    /// <summary>Total reputation points (the ledger sum).</summary>
    public int Score { get; set; }

    /// <summary>Number of assigned tasks the user has completed.</summary>
    public int TasksCompleted { get; set; }
}

/// <summary>The top of the leaderboard plus the current user's own standing.</summary>
public class LeaderboardDto
{
    public List<LeaderboardEntryDto> Entries { get; set; } = new();

    /// <summary>The requesting user's rank — set even when they're outside the top; null if unranked.</summary>
    public LeaderboardEntryDto? Me { get; set; }
}
