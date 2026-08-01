using Taskpilot.API.DTOs.Users;

namespace Taskpilot.API.Services;

/// <summary>Builds the community leaderboard from the reputation ledger.</summary>
public interface ILeaderboardService
{
    /// <summary>Top users by reputation plus the caller's own standing.</summary>
    Task<LeaderboardDto> GetAsync(Guid currentUserId, int limit = 20);
}
