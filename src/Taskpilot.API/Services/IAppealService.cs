using Taskpilot.API.Common;
using Taskpilot.API.DTOs.Admin;

namespace Taskpilot.API.Services;

/// <summary>
/// Moderation appeals. Users file appeals against their warnings; admins review them.
/// Approving an appeal removes the linked warning.
/// </summary>
public interface IAppealService
{
    /// <summary>Files an appeal for the given user.</summary>
    Task<Result<AppealDto>> CreateAsync(Guid userId, CreateAppealDto dto);

    /// <summary>Lists the user's own appeals (newest first).</summary>
    Task<Result<List<AppealDto>>> GetMineAsync(Guid userId);

    /// <summary>Lists appeals for the admin queue, optionally filtered by status.</summary>
    Task<Result<List<AppealDto>>> GetAllAsync(string? status = null);

    /// <summary>Resolves an appeal (approve removes the linked warning; reject keeps it).</summary>
    Task<Result<AppealDto>> ResolveAsync(Guid adminId, string? adminEmail, Guid appealId, ResolveAppealDto dto, string? ip);
}
