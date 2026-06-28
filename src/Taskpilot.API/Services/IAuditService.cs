using Taskpilot.API.Common;
using Taskpilot.API.DTOs.Admin;
using Taskpilot.API.DTOs.Common;

namespace Taskpilot.API.Services;

/// <summary>
/// Writes entries to the audit trail. A thin, append-only service used across the
/// app to record security- and moderation-relevant actions.
/// </summary>
public interface IAuditService
{
    /// <summary>
    /// Records one audit-trail entry.
    /// </summary>
    /// <param name="action">Stable dotted action code, e.g. "auth.login.success".</param>
    /// <param name="actorId">Id of the acting user, or null for system/anonymous actions.</param>
    /// <param name="actorEmail">Email snapshot of the actor, for readability.</param>
    /// <param name="entityType">Type of the affected entity (e.g. "User"), if any.</param>
    /// <param name="entityId">Id of the affected entity, if any.</param>
    /// <param name="details">Optional free-form context.</param>
    /// <param name="ipAddress">Caller IP address, when available.</param>
    Task LogAsync(
        string action,
        Guid? actorId = null,
        string? actorEmail = null,
        string? entityType = null,
        string? entityId = null,
        string? details = null,
        string? ipAddress = null);

    /// <summary>
    /// Returns a page of audit entries, newest first, optionally filtered by action.
    /// </summary>
    /// <param name="page">1-based page number.</param>
    /// <param name="pageSize">Items per page (clamped to a safe range).</param>
    /// <param name="action">Optional exact action filter (e.g. "auth.login.failed").</param>
    Task<Result<PagedResult<AuditLogDto>>> GetAsync(int page, int pageSize, string? action = null);
}
