using Taskpilot.API.Common;
using Taskpilot.API.DTOs.Admin;

namespace Taskpilot.API.Services;

/// <summary>
/// Admin-only operations: managing users (list, change role, ban/unban).
/// </summary>
public interface IAdminService
{
    /// <summary>Lists all users (newest first).</summary>
    Task<Result<List<AdminUserDto>>> GetAllUsersAsync();

    /// <summary>Changes a user's role.</summary>
    Task<Result> ChangeRoleAsync(Guid targetUserId, string role);

    /// <summary>
    /// Activates or deactivates a user (unban/ban). Banning also revokes the user's
    /// refresh tokens so they cannot keep their session alive.
    /// </summary>
    Task<Result> SetActiveAsync(Guid adminId, Guid targetUserId, bool isActive);
}
