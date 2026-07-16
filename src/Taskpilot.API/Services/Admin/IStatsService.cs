using Taskpilot.API.Common;
using Taskpilot.API.DTOs.Admin;
using Taskpilot.API.DTOs.Stats;

namespace Taskpilot.API.Services;

/// <summary>
/// Computes live site statistics (user/forum counts, online users, visitors).
/// </summary>
public interface IStatsService
{
    /// <summary>Full statistics for admins (includes anonymous-visitor analytics).</summary>
    Task<Result<AdminStatsDto>> GetFullStatsAsync();

    /// <summary>Public, safe-to-show statistics (no anonymous-visitor analytics).</summary>
    Task<Result<PublicStatsDto>> GetPublicStatsAsync();

    /// <summary>
    /// Per-day activity (signups, new topics, new tasks) over the last <paramref name="days"/>
    /// days, with missing days filled as zero. For the admin trend charts.
    /// </summary>
    Task<Result<List<DayActivityDto>>> GetActivityAsync(int days);
}
