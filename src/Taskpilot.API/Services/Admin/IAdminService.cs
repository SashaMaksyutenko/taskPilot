using Taskpilot.API.Common;
using Taskpilot.API.DTOs.Admin;
using Taskpilot.API.DTOs.Common;

namespace Taskpilot.API.Services;

/// <summary>
/// Admin-only operations: managing users (list, change role, ban/unban).
/// </summary>
public interface IAdminService
{
    /// <summary>
    /// Lists a page of users, optionally filtered by a name/email search, role and
    /// moderation status ("active", "banned" or "muted"), and ordered by
    /// "newest" (default), "oldest", "name" or "role".
    /// </summary>
    Task<Result<PagedResult<AdminUserDto>>> GetAllUsersAsync(
        int page = 1, int pageSize = 20, string? search = null, string? role = null, string? status = null, string? sort = null);

    /// <summary>Changes a user's role.</summary>
    Task<Result> ChangeRoleAsync(Guid targetUserId, string role);

    /// <summary>
    /// Activates or deactivates a user (unban/ban). Banning also revokes the user's
    /// refresh tokens so they cannot keep their session alive.
    /// </summary>
    Task<Result> SetActiveAsync(Guid adminId, Guid targetUserId, bool isActive, DateTime? bannedUntil = null);

    /// <summary>Mutes a user until the given UTC time, or clears the mute when null.</summary>
    Task<Result> SetMutedAsync(Guid adminId, Guid targetUserId, DateTime? mutedUntil);
}
