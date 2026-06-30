using Microsoft.EntityFrameworkCore;
using Taskpilot.API.Common;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Admin;
using Taskpilot.API.DTOs.Common;
using Taskpilot.API.Mappers;
using Taskpilot.API.Models;

namespace Taskpilot.API.Services;

/// <summary>
/// Admin user-management logic. Guarded at the controller with [Authorize(Roles="Admin")].
/// </summary>
public class AdminService : IAdminService
{
    private readonly TaskpilotDbContext _context;
    private readonly IWebhookService _webhooks;
    private readonly ILogger<AdminService> _logger;

    public AdminService(TaskpilotDbContext context, IWebhookService webhooks, ILogger<AdminService> logger)
    {
        _context = context;
        _webhooks = webhooks;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<PagedResult<AdminUserDto>>> GetAllUsersAsync(int page = 1, int pageSize = 20)
    {
        // Clamp paging to sane bounds.
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var total = await _context.Users.CountAsync();

        var users = await _context.Users
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync();

        return Result<PagedResult<AdminUserDto>>.Ok(new PagedResult<AdminUserDto>
        {
            Items = users.Select(UserMapper.ToAdminDto).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize,
        });
    }

    /// <inheritdoc />
    public async Task<Result> ChangeRoleAsync(Guid targetUserId, string role)
    {
        // The role must be one of the defined Role enum values.
        if (!Enum.TryParse<Role>(role, ignoreCase: true, out var parsedRole))
            return Result.Fail("Invalid role.");

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == targetUserId);
        if (user is null)
            return Result.Fail("User not found.");

        user.Role = parsedRole;
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Role changed. UserId: {UserId}, NewRole: {Role}", targetUserId, parsedRole);
        return Result.Ok();
    }

    /// <inheritdoc />
    public async Task<Result> SetActiveAsync(Guid adminId, Guid targetUserId, bool isActive, DateTime? bannedUntil = null)
    {
        // An admin must not lock themselves out.
        if (!isActive && adminId == targetUserId)
            return Result.Fail("You cannot ban yourself.");

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == targetUserId);
        if (user is null)
            return Result.Fail("User not found.");

        user.IsActive = isActive;
        // A ban may be temporary (BannedUntil set) or permanent (null). Clearing the
        // ban (unban) always resets the expiry.
        user.BannedUntil = isActive ? null : bannedUntil;
        user.UpdatedAt = DateTime.UtcNow;

        // When banning, revoke all active refresh tokens so the session truly ends.
        if (!isActive)
        {
            await _context.RefreshTokens
                .Where(rt => rt.UserId == targetUserId && rt.RevokedAtUtc == null)
                .ExecuteUpdateAsync(s => s.SetProperty(rt => rt.RevokedAtUtc, DateTime.UtcNow));
        }

        await _context.SaveChangesAsync();

        // Only the ban transition is broadcast (not unban).
        if (!isActive)
            await _webhooks.DispatchAsync(WebhookEvents.UserBanned, new
            {
                userId = user.Id,
                email = user.Email,
                name = user.Name,
                bannedAt = user.UpdatedAt,
            });

        _logger.LogInformation("User {Status}. UserId: {UserId}", isActive ? "unbanned" : "banned", targetUserId);
        return Result.Ok();
    }
}
