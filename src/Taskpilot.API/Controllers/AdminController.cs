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

    public AdminController(IAdminService adminService, IAuditService audit)
    {
        _adminService = adminService;
        _audit = audit;
    }

    /// <summary>Lists all users.</summary>
    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
        var result = await _adminService.GetAllUsersAsync();
        return Ok(result.Value);
    }

    /// <summary>Changes a user's role.</summary>
    [HttpPut("users/{userId:guid}/role")]
    public async Task<IActionResult> ChangeRole(Guid userId, [FromBody] ChangeRoleDto dto)
    {
        var result = await _adminService.ChangeRoleAsync(userId, dto.Role);
        return result.Succeeded
            ? Ok(new { message = $"Role changed to {dto.Role}." })
            : BadRequest(new { error = result.Error });
    }

    /// <summary>Bans (deactivates) a user.</summary>
    [HttpPost("users/{userId:guid}/ban")]
    public async Task<IActionResult> Ban(Guid userId)
    {
        var adminId = CurrentUserId();
        if (adminId is null) return Unauthorized();

        var result = await _adminService.SetActiveAsync(adminId.Value, userId, isActive: false);
        return result.Succeeded
            ? Ok(new { message = "User banned." })
            : BadRequest(new { error = result.Error });
    }

    /// <summary>Unbans (reactivates) a user.</summary>
    [HttpPost("users/{userId:guid}/unban")]
    public async Task<IActionResult> Unban(Guid userId)
    {
        var adminId = CurrentUserId();
        if (adminId is null) return Unauthorized();

        var result = await _adminService.SetActiveAsync(adminId.Value, userId, isActive: true);
        return result.Succeeded
            ? Ok(new { message = "User unbanned." })
            : BadRequest(new { error = result.Error });
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
