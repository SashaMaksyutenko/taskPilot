using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Taskpilot.API.DTOs.Admin;
using Taskpilot.API.Services;

namespace Taskpilot.API.Controllers;

/// <summary>
/// Admin-only user management. Every endpoint requires the "Admin" role
/// (enforced by RBAC via the role claim in the JWT).
/// </summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin")]
public class AdminController : BaseApiController
{
    private readonly IAdminService _adminService;
    private readonly IAuditService _audit;
    private readonly IStatsService _stats;
    private readonly IOverdueService _overdue;

    public AdminController(IAdminService adminService, IAuditService audit, IStatsService stats, IOverdueService overdue)
    {
        _adminService = adminService;
        _audit = audit;
        _stats = stats;
        _overdue = overdue;
    }

    /// <summary>Lists a page of users.</summary>
    [HttpGet("users")]
    public async Task<IActionResult> GetUsers([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _adminService.GetAllUsersAsync(page, pageSize);
        return Ok(result.Value);
    }

    /// <summary>Live site statistics: user counts, online users, anonymous visitors.</summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var result = await _stats.GetFullStatsAsync();
        return Ok(result.Value);
    }

    /// <summary>Runs the overdue-tasks check now (otherwise it runs on a timer).</summary>
    [HttpPost("overdue-check")]
    public async Task<IActionResult> RunOverdueCheck()
    {
        var processed = await _overdue.ProcessOverdueAsync();
        return Ok(new { processed });
    }

    /// <summary>Changes a user's role.</summary>
    [HttpPut("users/{userId:guid}/role")]
    public async Task<IActionResult> ChangeRole(Guid userId, [FromBody] ChangeRoleDto dto)
    {
        var result = await _adminService.ChangeRoleAsync(userId, dto.Role);
        if (!result.Succeeded)
            return BadRequest(new { error = result.Error });

        // Record who changed whose role and to what.
        await _audit.LogAsync("user.role.changed", actorId: CurrentUserId(), actorEmail: CurrentUserEmail(),
            entityType: "User", entityId: userId.ToString(), details: $"role -> {dto.Role}", ipAddress: ClientIp());
        return Ok(new { message = $"Role changed to {dto.Role}." });
    }

    /// <summary>Bans (deactivates) a user.</summary>
    [HttpPost("users/{userId:guid}/ban")]
    public async Task<IActionResult> Ban(Guid userId)
    {
        var adminId = CurrentUserId();
        if (adminId is null) return Unauthorized();

        var result = await _adminService.SetActiveAsync(adminId.Value, userId, isActive: false);
        if (!result.Succeeded)
            return BadRequest(new { error = result.Error });

        await _audit.LogAsync("user.banned", actorId: adminId, actorEmail: CurrentUserEmail(),
            entityType: "User", entityId: userId.ToString(), ipAddress: ClientIp());
        return Ok(new { message = "User banned." });
    }

    /// <summary>Unbans (reactivates) a user.</summary>
    [HttpPost("users/{userId:guid}/unban")]
    public async Task<IActionResult> Unban(Guid userId)
    {
        var adminId = CurrentUserId();
        if (adminId is null) return Unauthorized();

        var result = await _adminService.SetActiveAsync(adminId.Value, userId, isActive: true);
        if (!result.Succeeded)
            return BadRequest(new { error = result.Error });

        await _audit.LogAsync("user.unbanned", actorId: adminId, actorEmail: CurrentUserEmail(),
            entityType: "User", entityId: userId.ToString(), ipAddress: ClientIp());
        return Ok(new { message = "User unbanned." });
    }

    /// <summary>Returns a page of audit-trail entries (newest first), optionally filtered by action.</summary>
    [HttpGet("audit")]
    public async Task<IActionResult> GetAudit(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? action = null)
    {
        var result = await _audit.GetAsync(page, pageSize, action);
        return Ok(result.Value);
    }
}
