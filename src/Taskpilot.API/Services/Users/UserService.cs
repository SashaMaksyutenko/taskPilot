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
        // Read-only lookup, projected to just the columns PublicProfileDto needs — this is a
        // public endpoint, so the password hash and 2FA secret have no business being loaded.
        var user = await _context.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new User
            {
                Id = u.Id,
                Name = u.Name,
                Role = u.Role,
                AvatarFileId = u.AvatarFileId,
                Title = u.Title,
                Bio = u.Bio,
                Location = u.Location,
                ShowEmail = u.ShowEmail,
                Email = u.Email, // gated by ShowEmail inside the mapper
                Website = u.Website,
                LinkedIn = u.LinkedIn,
                GitHub = u.GitHub,
                Phone = u.Phone,
                CreatedAt = u.CreatedAt,
            })
            .FirstOrDefaultAsync();

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
    /// (forum solutions/votes, completed marketplace tasks, review stars, minus a penalty
    /// for late/overdue tasks). No ledger — it is recomputed on demand so it always
    /// reflects current data.
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

        var timelinessBonus = await ComputeTimelinessBonusAsync(userId);
        var latePenalty = await ComputeLatePenaltyAsync(userId);

        var points = solutions * 15 + upvotes * 2 + completedTasks * 10 + reviewScore * 3
                     + timelinessBonus - latePenalty;
        dto.ReputationPoints = Math.Max(0, points);

        var badges = new List<string>();
        if (solutions >= 5) badges.Add("solver");
        if (upvotes >= 25) badges.Add("contributor");
        if (completedTasks >= 3 && (dto.AverageRating ?? 0) >= 4) badges.Add("freelancer");
        if (dto.ReputationPoints >= 100) badges.Add("veteran");
        dto.Badges = badges;
    }

    /// <summary>
    /// Sums the reputation bonus for the user's timely work: tasks assigned to them that
    /// were finished on or before their deadline. Finishing a full day or more early is
    /// worth +15, otherwise on-time is worth +10.
    /// </summary>
    private async Task<int> ComputeTimelinessBonusAsync(Guid userId)
    {
        // Finished tasks that met their deadline.
        var onTimeTasks = await _context.ProjectTasks
            .Where(t => t.AssigneeId == userId
                        && t.Status == ProjectTaskStatus.Done
                        && t.Deadline != null
                        && t.CompletedAt != null
                        && t.CompletedAt <= t.Deadline)
            .Select(t => new { t.Deadline, t.CompletedAt })
            .ToListAsync();

        var bonus = 0;
        foreach (var t in onTimeTasks)
        {
            var daysEarly = (t.Deadline!.Value - t.CompletedAt!.Value).TotalDays;
            bonus += daysEarly >= 1 ? 15 : 10; // a full day early is worth more
        }

        return bonus;
    }

    /// <summary>
    /// Sums the reputation penalty for the user's late work: tasks assigned to them that
    /// were either finished after their deadline or are still overdue. Each late task
    /// costs points on a tier by how many days late it is (1d=−2, 3d=−5, 5d+=−10).
    /// </summary>
    private async Task<int> ComputeLatePenaltyAsync(Guid userId)
    {
        var now = DateTime.UtcNow;

        // Assigned tasks with a deadline that are either done-late or still overdue.
        var lateTasks = await _context.ProjectTasks
            .Where(t => t.AssigneeId == userId
                        && t.Deadline != null
                        && ((t.Status == ProjectTaskStatus.Done && t.CompletedAt != null && t.CompletedAt > t.Deadline)
                            || (t.Status != ProjectTaskStatus.Done && t.Deadline < now)))
            .Select(t => new { t.Status, t.Deadline, t.CompletedAt })
            .ToListAsync();

        var penalty = 0;
        foreach (var t in lateTasks)
        {
            // For a finished task measure to completion; for an unfinished one, to now.
            var reference = t.Status == ProjectTaskStatus.Done ? t.CompletedAt!.Value : now;
            var daysLate = (reference - t.Deadline!.Value).TotalDays;
            penalty += LatePenalty(daysLate);
        }

        return penalty;
    }

    /// <summary>Maps how many days a task is late to a reputation penalty (tiered).</summary>
    private static int LatePenalty(double daysLate) => daysLate switch
    {
        >= 5 => 10,
        >= 3 => 5,
        >= 1 => 2,
        _ => 0, // less than a full day late costs nothing
    };

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

        // Remember the outgoing avatar: nothing else references it, so once the pointer
        // moves it would be orphaned (row + bytes) forever.
        var previousAvatarId = user.AvatarFileId;

        user.AvatarFileId = saved.Value!.Id;
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        await DeleteReplacedAvatarAsync(previousAvatarId, userId);

        _logger.LogInformation("Avatar set. UserId: {UserId}, FileId: {FileId}", userId, user.AvatarFileId);
        return Result<UserDto>.Ok(UserMapper.ToDto(user));
    }

    /// <inheritdoc />
    public async Task<Result<UserDto>> RemoveAvatarAsync(Guid userId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null)
            return Result<UserDto>.Fail("User not found.");

        var previousAvatarId = user.AvatarFileId;

        user.AvatarFileId = null;
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        await DeleteReplacedAvatarAsync(previousAvatarId, userId);

        _logger.LogInformation("Avatar removed. UserId: {UserId}", userId);
        return Result<UserDto>.Ok(UserMapper.ToDto(user));
    }

    /// <summary>
    /// Deletes an avatar image the user no longer points at, so replacing or removing an
    /// avatar does not leak a file row and its bytes.
    /// </summary>
    /// <remarks>
    /// Best-effort on purpose: the avatar change itself already succeeded and has been
    /// saved, so a failure to clean up the old image must not fail the caller's request.
    /// </remarks>
    /// <param name="previousAvatarId">The outgoing avatar's file id; null when there was none.</param>
    /// <param name="userId">Owner of the avatar — also the file's uploader.</param>
    private async Task DeleteReplacedAvatarAsync(Guid? previousAvatarId, Guid userId)
    {
        if (previousAvatarId is not { } fileId)
            return;

        var deleted = await _files.DeleteAsync(fileId, userId);
        if (!deleted.Succeeded)
            _logger.LogWarning("Could not delete the replaced avatar. FileId: {FileId}, Reason: {Reason}",
                fileId, deleted.Error);
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAccountAsync(Guid userId, string password)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null)
            return Result.Fail("User not found.");

        // Require the current password to confirm the (irreversible) closure.
        if (string.IsNullOrEmpty(user.PasswordHash) || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            return Result.Fail("Password is incorrect.");

        // Scrub personal data and disable login. Email is replaced with a unique
        // placeholder so the uniqueness constraint still holds.
        user.Name = "Deleted user";
        user.Email = $"deleted-{user.Id:N}@deleted.local";
        user.PasswordHash = null;
        user.Title = null;
        user.Bio = null;
        user.Location = null;
        user.Website = null;
        user.LinkedIn = null;
        user.GitHub = null;
        user.Phone = null;
        user.ShowEmail = false;
        user.AvatarFileId = null;
        user.TwoFactorEnabled = false;
        user.TwoFactorSecret = null;
        user.IsActive = false;
        user.UpdatedAt = DateTime.UtcNow;

        // Remove standalone personal records (authored content is kept, anonymized).
        _context.Notes.RemoveRange(_context.Notes.Where(n => n.OwnerId == userId));
        _context.NotificationPreferences.RemoveRange(_context.NotificationPreferences.Where(p => p.UserId == userId));
        _context.UserBackupCodes.RemoveRange(_context.UserBackupCodes.Where(c => c.UserId == userId));
        _context.RefreshTokens.RemoveRange(_context.RefreshTokens.Where(rt => rt.UserId == userId));
        _context.ProjectMembers.RemoveRange(_context.ProjectMembers.Where(m => m.UserId == userId));
        _context.UserWarnings.RemoveRange(_context.UserWarnings.Where(w => w.UserId == userId));
        _context.Appeals.RemoveRange(_context.Appeals.Where(a => a.UserId == userId));

        await _context.SaveChangesAsync();

        _logger.LogInformation("Account closed and anonymized. UserId: {UserId}", userId);
        return Result.Ok();
    }

    /// <inheritdoc />
    public async Task<Result<object>> ExportDataAsync(Guid userId)
    {
        var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null)
            return Result<object>.Fail("User not found.");

        var profile = new
        {
            user.Id,
            user.Name,
            user.Email,
            user.Role,
            user.Title,
            user.Bio,
            user.Location,
            user.Website,
            user.LinkedIn,
            user.GitHub,
            user.Phone,
            user.CreatedAt,
        };

        var notes = await _context.Notes
            .Where(n => n.OwnerId == userId)
            .OrderBy(n => n.CreatedAt)
            .Select(n => new { n.Title, n.Content, n.IsPinned, n.CreatedAt })
            .AsNoTracking().ToListAsync();

        var projects = await _context.Projects
            .Where(p => p.OwnerId == userId)
            .OrderBy(p => p.CreatedAt)
            .Select(p => new
            {
                p.Name,
                p.Description,
                p.CreatedAt,
                Tasks = p.Tasks.OrderBy(t => t.CreatedAt).Select(t => new
                {
                    t.Title,
                    Status = t.Status.ToString(),
                    Priority = t.Priority.ToString(),
                    t.Deadline,
                    t.CreatedAt,
                }).ToList(),
            })
            .AsNoTracking().ToListAsync();

        var forumTopics = await _context.ForumTopics
            .Where(f => f.AuthorId == userId)
            .OrderBy(f => f.CreatedAt)
            .Select(f => new { f.Title, f.Body, f.CreatedAt })
            .AsNoTracking().ToListAsync();

        var forumReplies = await _context.ForumReplies
            .Where(r => r.AuthorId == userId)
            .OrderBy(r => r.CreatedAt)
            .Select(r => new { r.Body, r.CreatedAt })
            .AsNoTracking().ToListAsync();

        var taskComments = await _context.TaskComments
            .Where(c => c.AuthorId == userId)
            .OrderBy(c => c.CreatedAt)
            .Select(c => new { c.Body, c.CreatedAt })
            .AsNoTracking().ToListAsync();

        _logger.LogInformation("Data export built. UserId: {UserId}", userId);
        return Result<object>.Ok(new
        {
            exportedAt = DateTime.UtcNow,
            profile,
            notes,
            projects,
            forumTopics,
            forumReplies,
            taskComments,
        });
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
