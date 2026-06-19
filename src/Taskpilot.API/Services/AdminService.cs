using Microsoft.EntityFrameworkCore;
using Taskpilot.API.Common;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Admin;
using Taskpilot.API.Mappers;
using Taskpilot.API.Models;

namespace Taskpilot.API.Services;

/// <summary>
/// Admin user-management logic. Guarded at the controller with [Authorize(Roles="Admin")].
/// </summary>
public class AdminService : IAdminService
{
    private readonly TaskpilotDbContext _context;
    private readonly ILogger<AdminService> _logger;

    public AdminService(TaskpilotDbContext context, ILogger<AdminService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<List<AdminUserDto>>> GetAllUsersAsync()
    {
        var users = await _context.Users
            .OrderByDescending(u => u.CreatedAt)
            .AsNoTracking()
            .ToListAsync();

        return Result<List<AdminUserDto>>.Ok(users.Select(UserMapper.ToAdminDto).ToList());
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
    public async Task<Result> SetActiveAsync(Guid adminId, Guid targetUserId, bool isActive)
    {
        // An admin must not lock themselves out.
        if (!isActive && adminId == targetUserId)
            return Result.Fail("You cannot ban yourself.");

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == targetUserId);
        if (user is null)
            return Result.Fail("User not found.");

        user.IsActive = isActive;
        user.UpdatedAt = DateTime.UtcNow;

        // When banning, revoke all active refresh tokens so the session truly ends.
        if (!isActive)
        {
            await _context.RefreshTokens
                .Where(rt => rt.UserId == targetUserId && rt.RevokedAtUtc == null)
                .ExecuteUpdateAsync(s => s.SetProperty(rt => rt.RevokedAtUtc, DateTime.UtcNow));
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("User {Status}. UserId: {UserId}", isActive ? "unbanned" : "banned", targetUserId);
        return Result.Ok();
    }
}
