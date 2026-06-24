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
        user.Website = dto.Website?.Trim();
        user.LinkedIn = dto.LinkedIn?.Trim();
        user.GitHub = dto.GitHub?.Trim();
        user.Phone = dto.Phone?.Trim();
        user.ShowEmail = dto.ShowEmail;
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

    /// <inheritdoc />
    public async Task<Result<PublicProfileDto>> GetPublicProfileAsync(Guid userId)
    {
        // Read-only lookup of the public profile.
        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user is null)
            return Result<PublicProfileDto>.Fail("User not found.");

        var dto = UserMapper.ToPublicProfile(user);

        // Aggregate the marketplace reviews this user has received.
        var stars = await _context.Reviews
            .Where(r => r.RateeId == userId)
            .Select(r => r.Stars)
            .ToListAsync();
        dto.ReviewCount = stars.Count;
        dto.AverageRating = stars.Count > 0 ? Math.Round(stars.Average(), 1) : null;

        return Result<PublicProfileDto>.Ok(dto);
    }

    /// <inheritdoc />
    public async Task<Result<List<UserSearchResultDto>>> SearchUsersAsync(Guid currentUserId, string query)
    {
        var term = query.Trim();
        // Require at least 2 characters to avoid returning the whole directory.
        if (term.Length < 2)
            return Result<List<UserSearchResultDto>>.Ok(new List<UserSearchResultDto>());

        var pattern = $"%{term}%";
        var users = await _context.Users
            .Where(u => u.IsActive
                        && u.Id != currentUserId
                        && (EF.Functions.ILike(u.Name, pattern) || EF.Functions.ILike(u.Email, pattern)))
            .OrderBy(u => u.Name)
            .Take(10)
            .Select(u => new UserSearchResultDto { Id = u.Id, Name = u.Name, Title = u.Title })
            .AsNoTracking()
            .ToListAsync();

        return Result<List<UserSearchResultDto>>.Ok(users);
    }
}
