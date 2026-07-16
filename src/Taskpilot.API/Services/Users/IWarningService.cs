using Taskpilot.API.Common;
using Taskpilot.API.DTOs.Admin;

namespace Taskpilot.API.Services;

/// <summary>
/// Moderation warnings. Admins issue warnings; reaching the threshold auto-bans the
/// user. Users can read their own warnings.
/// </summary>
public interface IWarningService
{
    /// <summary>Issues a warning to a user and escalates to a ban at the threshold.</summary>
    Task<Result<IssueWarningResultDto>> IssueAsync(Guid adminId, string? adminEmail, Guid targetUserId, IssueWarningDto dto, string? ip);

    /// <summary>Lists a user's warnings (newest first).</summary>
    Task<Result<List<WarningDto>>> GetForUserAsync(Guid userId);
}
