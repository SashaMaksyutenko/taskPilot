using Taskpilot.API.Common;
using Taskpilot.API.DTOs.Auth;
using Taskpilot.API.DTOs.Users;

namespace Taskpilot.API.Services;

/// <summary>
/// User profile operations (update profile, change password).
/// </summary>
public interface IUserService
{
    /// <summary>Updates the user's profile fields.</summary>
    Task<Result<UserDto>> UpdateProfileAsync(Guid userId, UpdateProfileDto dto);

    /// <summary>Changes the user's password after verifying the current one.</summary>
    Task<Result> ChangePasswordAsync(Guid userId, ChangePasswordDto dto);

    /// <summary>
    /// Returns the public profile of any user by id. Pass the viewer's id to mark which
    /// skills they have already endorsed.
    /// </summary>
    Task<Result<PublicProfileDto>> GetPublicProfileAsync(Guid userId, Guid? viewerId = null);

    /// <summary>Toggles the caller's endorsement of one of a user's listed skills.</summary>
    Task<Result<SkillEndorsementResultDto>> ToggleSkillEndorsementAsync(Guid endorserId, Guid userId, string skill);

    /// <summary>Searches active users by name or email, excluding the caller.</summary>
    Task<Result<List<UserSearchResultDto>>> SearchUsersAsync(Guid currentUserId, string query);

    /// <summary>Sets the user's avatar from an uploaded image and returns the updated profile.</summary>
    Task<Result<UserDto>> SetAvatarAsync(Guid userId, IFormFile file);

    /// <summary>Clears the user's avatar.</summary>
    Task<Result<UserDto>> RemoveAvatarAsync(Guid userId);

    /// <summary>Resolves a user's avatar image for download (public).</summary>
    Task<Result<FileDownload>> GetAvatarAsync(Guid userId);

    /// <summary>Builds a portable copy of the user's personal data (GDPR export).</summary>
    Task<Result<object>> ExportDataAsync(Guid userId);

    /// <summary>
    /// Closes the account: verifies the password, scrubs personal data, disables login
    /// and removes standalone personal records. Authored content stays, anonymized.
    /// </summary>
    Task<Result> DeleteAccountAsync(Guid userId, string password);
}
