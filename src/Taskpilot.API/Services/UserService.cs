using Microsoft.EntityFrameworkCore;
using Taskpilot.API.Common;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Auth;
using Taskpilot.API.DTOs.Users;
using Taskpilot.API.Mappers;

namespace Taskpilot.API.Services;

/// <summary>
/// Handles user profile management: updating profile fields and changing the password.
/// </summary>
public class UserService : IUserService
{
    private readonly TaskpilotDbContext _context;
    private readonly ILogger<UserService> _logger;

    public UserService(TaskpilotDbContext context, ILogger<UserService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<UserDto>> UpdateProfileAsync(Guid userId, UpdateProfileDto dto)
    {
        _logger.LogInformation("UpdateProfile. UserId: {UserId}", userId);

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null)
            return Result<UserDto>.Fail("User not found.");

        user.Name = dto.Name.Trim();
        user.Title = dto.Title?.Trim();
        user.Bio = dto.Bio?.Trim();
        user.Location = dto.Location?.Trim();
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        _logger.LogInformation("Profile updated. UserId: {UserId}", userId);
        return Result<UserDto>.Ok(UserMapper.ToDto(user));
    }

    /// <inheritdoc />
    public async Task<Result> ChangePasswordAsync(Guid userId, ChangePasswordDto dto)
    {
        _logger.LogInformation("ChangePassword. UserId: {UserId}", userId);

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null)
            return Result.Fail("User not found.");

        // OAuth-only accounts have no password to change.
        if (string.IsNullOrEmpty(user.PasswordHash))
            return Result.Fail("This account has no password set.");

        // Verify the current password before allowing a change.
        if (!BCrypt.Net.BCrypt.Verify(dto.CurrentPassword, user.PasswordHash))
        {
            _logger.LogWarning("ChangePassword failed: wrong current password. UserId: {UserId}", userId);
            return Result.Fail("Current password is incorrect.");
        }

        // Hash and store the new password.
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Password changed. UserId: {UserId}", userId);
        return Result.Ok();
    }
}
