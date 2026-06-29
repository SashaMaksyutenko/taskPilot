using Microsoft.EntityFrameworkCore;
using Taskpilot.API.Common;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Auth;
using Taskpilot.API.DTOs.Users;
using Taskpilot.API.Mappers;
using Taskpilot.API.Models;

namespace Taskpilot.API.Services;

/// <summary>
/// Handles user profile management: updating profile fields and changing the password.
/// </summary>
public class UserService : IUserService
{
    // Avatars must be images and reasonably small.
    private const long MaxAvatarBytes = 5 * 1024 * 1024;

    private readonly TaskpilotDbContext _context;
    private readonly IFileService _files;
    private readonly ILogger<UserService> _logger;

    public UserService(TaskpilotDbContext context, IFileService files, ILogger<UserService> logger)
    {
        _context = context;
        _files = files;
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

        await ApplyReputationAsync(dto, userId, stars);

        return Result<PublicProfileDto>.Ok(dto);
    }

    /// <summary>
    /// Computes a derived reputation score and badges from the user's existing activity
    /// (forum solutions/votes, completed marketplace tasks, review stars). No ledger —
    /// it is recomputed on demand so it always reflects current data.
    /// </summary>
    private async Task ApplyReputationAsync(PublicProfileDto dto, Guid userId, List<int> reviewStars)
    {
        var solutions = await _context.ForumReplies
            .CountAsync(r => r.AuthorId == userId && r.IsSolution);

        // Net up/down votes across all replies the user authored.
        var upvotes = await _context.ForumVotes
            .Where(v => v.Reply.AuthorId == userId)
            .SumAsync(v => (int?)v.Value) ?? 0;

        var completedTasks = await _context.MarketplaceTasks
            .CountAsync(t => t.AssigneeId == userId && t.Status == MarketplaceTaskStatus.Completed);

        // Reviews above 3★ add, below 3★ subtract.
        var reviewScore = reviewStars.Sum(s => s - 3);

        var points = solutions * 15 + upvotes * 2 + completedTasks * 10 + reviewScore * 3;
        dto.ReputationPoints = Math.Max(0, points);

        var badges = new List<string>();
        if (solutions >= 5) badges.Add("solver");
        if (upvotes >= 25) badges.Add("contributor");
        if (completedTasks >= 3 && (dto.AverageRating ?? 0) >= 4) badges.Add("freelancer");
        if (dto.ReputationPoints >= 100) badges.Add("veteran");
        dto.Badges = badges;
    }

    /// <inheritdoc />
    public async Task<Result<List<UserSearchResultDto>>> SearchUsersAsync(Guid currentUserId, string query)
    {
        var term = query.Trim();
        // Require at least 2 characters to avoid returning the whole directory.
        if (term.Length < 2)
            return Result<List<UserSearchResultDto>>.Ok(new List<UserSearchResultDto>());

        var pattern = $"%{term}%";
        var matches = await _context.Users
            .Where(u => u.IsActive
                        && u.Id != currentUserId
                        && (EF.Functions.ILike(u.Name, pattern) || EF.Functions.ILike(u.Email, pattern)))
            .OrderBy(u => u.Name)
            .Take(10)
            .AsNoTracking()
            .ToListAsync();

        // Build the DTOs in memory so the avatar URL can be composed (EF can't
        // translate the string interpolation in UserMapper.AvatarUrl).
        var users = matches
            .Select(u => new UserSearchResultDto
            {
                Id = u.Id,
                Name = u.Name,
                Title = u.Title,
                AvatarUrl = UserMapper.AvatarUrl(u),
            })
            .ToList();

        return Result<List<UserSearchResultDto>>.Ok(users);
    }

    /// <inheritdoc />
    public async Task<Result<UserDto>> SetAvatarAsync(Guid userId, IFormFile file)
    {
        if (file is null || file.Length == 0)
            return Result<UserDto>.Fail("No file was provided.");

        if (file.Length > MaxAvatarBytes)
            return Result<UserDto>.Fail("Avatar exceeds the 5 MB limit.");

        // Only images may be used as avatars.
        if (string.IsNullOrWhiteSpace(file.ContentType) || !file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return Result<UserDto>.Fail("Avatar must be an image.");

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null)
            return Result<UserDto>.Fail("User not found.");

        // Reuse the generic file storage to persist the image bytes + metadata.
        var saved = await _files.SaveAsync(file, userId);
        if (!saved.Succeeded)
            return Result<UserDto>.Fail(saved.Error!);

        user.AvatarFileId = saved.Value!.Id;
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Avatar set. UserId: {UserId}, FileId: {FileId}", userId, user.AvatarFileId);
        return Result<UserDto>.Ok(UserMapper.ToDto(user));
    }

    /// <inheritdoc />
    public async Task<Result<UserDto>> RemoveAvatarAsync(Guid userId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null)
            return Result<UserDto>.Fail("User not found.");

        user.AvatarFileId = null;
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Avatar removed. UserId: {UserId}", userId);
        return Result<UserDto>.Ok(UserMapper.ToDto(user));
    }

    /// <inheritdoc />
    public async Task<Result<FileDownload>> GetAvatarAsync(Guid userId)
    {
        var fileId = await _context.Users
            .Where(u => u.Id == userId)
            .Select(u => u.AvatarFileId)
            .FirstOrDefaultAsync();

        if (fileId is null)
            return Result<FileDownload>.Fail("Avatar not found.");

        return await _files.GetForDownloadAsync(fileId.Value);
    }
}
