using FluentValidation;
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
    private readonly IWarningService _warnings;
    private readonly IAppealService _appeals;
    private readonly IOrganizationSettingsService _settings;
    private readonly IValidator<IssueWarningDto> _issueWarningValidator;

    public AdminController(
        IAdminService adminService,
        IAuditService audit,
        IStatsService stats,
        IOverdueService overdue,
        IWarningService warnings,
        IAppealService appeals,
        IOrganizationSettingsService settings,
        IValidator<IssueWarningDto> issueWarningValidator)
    {
        _adminService = adminService;
        _audit = audit;
        _stats = stats;
        _overdue = overdue;
        _warnings = warnings;
        _appeals = appeals;
        _settings = settings;
        _issueWarningValidator = issueWarningValidator;
    }

    /// <summary>Lists a page of users, optionally filtered by search, role and status.</summary>
    [HttpGet("users")]
    public async Task<IActionResult> GetUsers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? role = null,
        [FromQuery] string? status = null,
        [FromQuery] string? sort = null)
    {
        var result = await _adminService.GetAllUsersAsync(page, pageSize, search, role, status, sort);
        return Ok(result.Value);
    }

    /// <summary>Live site statistics: user counts, online users, anonymous visitors.</summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var result = await _stats.GetFullStatsAsync();
        return Ok(result.Value);
    }

    /// <summary>Per-day activity (signups, topics, tasks) over the last N days, for trend charts.</summary>
    [HttpGet("activity")]
    public async Task<IActionResult> GetActivity([FromQuery] int days = 30)
    {
        var result = await _stats.GetActivityAsync(days);
        return Ok(result.Value);
    }

    /// <summary>Returns the organization settings (storage limits) plus current usage.</summary>
    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings()
    {
        var result = await _settings.GetAsync();
        return Ok(result);
    }

    /// <summary>Updates the organization's storage limits (leaves feature flags untouched).</summary>
    [HttpPut("settings/storage")]
    public async Task<IActionResult> UpdateStorage([FromBody] UpdateStorageDto dto)
    {
        var adminId = CurrentUserId();
        if (adminId is null) return Unauthorized();

        var result = await _settings.UpdateStorageAsync(dto, adminId.Value, CurrentUserEmail(), ClientIp());
        return result.Succeeded ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }

    /// <summary>Updates the organization's feature flags (leaves storage limits untouched).</summary>
    [HttpPut("settings/features")]
    public async Task<IActionResult> UpdateFeatures([FromBody] UpdateFeaturesDto dto)
    {
        var adminId = CurrentUserId();
        if (adminId is null) return Unauthorized();

        var result = await _settings.UpdateFeaturesAsync(dto, adminId.Value, CurrentUserEmail(), ClientIp());
        return result.Succeeded ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }

    /// <summary>Updates the registration email-domain allowlist (leaves storage and features untouched).</summary>
    [HttpPut("settings/registration")]
    public async Task<IActionResult> UpdateRegistration([FromBody] UpdateRegistrationDto dto)
    {
        var adminId = CurrentUserId();
        if (adminId is null) return Unauthorized();

        var result = await _settings.UpdateRegistrationAsync(dto, adminId.Value, CurrentUserEmail(), ClientIp());
        return result.Succeeded ? Ok(result.Value) : BadRequest(new { error = result.Error });
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

    /// <summary>Bans (deactivates) a user, permanently or for a number of days.</summary>
    [HttpPost("users/{userId:guid}/ban")]
    public async Task<IActionResult> Ban(Guid userId, [FromBody] BanUserDto? dto = null)
    {
        var adminId = CurrentUserId();
        if (adminId is null) return Unauthorized();

        // A positive day count makes it temporary; otherwise it is permanent.
        DateTime? bannedUntil = dto?.Days is > 0 ? DateTime.UtcNow.AddDays(dto!.Days!.Value) : null;

        var result = await _adminService.SetActiveAsync(adminId.Value, userId, isActive: false, bannedUntil);
        if (!result.Succeeded)
            return BadRequest(new { error = result.Error });

        await _audit.LogAsync("user.banned", actorId: adminId, actorEmail: CurrentUserEmail(),
            entityType: "User", entityId: userId.ToString(),
            details: bannedUntil is { } until ? $"until {until:u}" : "permanent", ipAddress: ClientIp());
        return Ok(new { message = "User banned.", bannedUntil });
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

    /// <summary>Issues a moderation warning to a user (auto-bans at the threshold).</summary>
    [HttpPost("users/{userId:guid}/warnings")]
    public async Task<IActionResult> IssueWarning(Guid userId, [FromBody] IssueWarningDto dto)
    {
        var adminId = CurrentUserId();
        if (adminId is null) return Unauthorized();

        var validation = await _issueWarningValidator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(new { error = validation.Errors[0].ErrorMessage });

        var result = await _warnings.IssueAsync(adminId.Value, CurrentUserEmail(), userId, dto, ClientIp());
        return result.Succeeded
            ? Ok(result.Value)
            : BadRequest(new { error = result.Error });
    }

    /// <summary>Lists a user's moderation warnings (newest first).</summary>
    [HttpGet("users/{userId:guid}/warnings")]
    public async Task<IActionResult> GetUserWarnings(Guid userId)
    {
        var result = await _warnings.GetForUserAsync(userId);
        return Ok(result.Value);
    }

    /// <summary>Mutes a user (read-only: cannot post) for a number of days (default 1).</summary>
    [HttpPost("users/{userId:guid}/mute")]
    public async Task<IActionResult> Mute(Guid userId, [FromBody] MuteUserDto? dto = null)
    {
        var adminId = CurrentUserId();
        if (adminId is null) return Unauthorized();

        var days = dto?.Days is > 0 ? dto!.Days!.Value : 1;
        var mutedUntil = DateTime.UtcNow.AddDays(days);

        var result = await _adminService.SetMutedAsync(adminId.Value, userId, mutedUntil);
        if (!result.Succeeded)
            return BadRequest(new { error = result.Error });

        await _audit.LogAsync("user.muted", actorId: adminId, actorEmail: CurrentUserEmail(),
            entityType: "User", entityId: userId.ToString(), details: $"until {mutedUntil:u}", ipAddress: ClientIp());
        return Ok(new { message = "User muted.", mutedUntil });
    }

    /// <summary>Lifts a user's mute.</summary>
    [HttpPost("users/{userId:guid}/unmute")]
    public async Task<IActionResult> Unmute(Guid userId)
    {
        var adminId = CurrentUserId();
        if (adminId is null) return Unauthorized();

        var result = await _adminService.SetMutedAsync(adminId.Value, userId, null);
        if (!result.Succeeded)
            return BadRequest(new { error = result.Error });

        await _audit.LogAsync("user.unmuted", actorId: adminId, actorEmail: CurrentUserEmail(),
            entityType: "User", entityId: userId.ToString(), ipAddress: ClientIp());
        return Ok(new { message = "User unmuted." });
    }

    /// <summary>Lists moderation appeals (pending first), optionally filtered by status.</summary>
    [HttpGet("appeals")]
    public async Task<IActionResult> GetAppeals([FromQuery] string? status = null)
    {
        var result = await _appeals.GetAllAsync(status);
        return Ok(result.Value);
    }

    /// <summary>Resolves an appeal: approve removes the linked warning, reject keeps it.</summary>
    [HttpPost("appeals/{appealId:guid}/resolve")]
    public async Task<IActionResult> ResolveAppeal(Guid appealId, [FromBody] ResolveAppealDto dto)
    {
        var adminId = CurrentUserId();
        if (adminId is null) return Unauthorized();

        var result = await _appeals.ResolveAsync(adminId.Value, CurrentUserEmail(), appealId, dto, ClientIp());
        return result.Succeeded
            ? Ok(result.Value)
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
