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

    /// <summary>Returns the public profile of any user by id.</summary>
    Task<Result<PublicProfileDto>> GetPublicProfileAsync(Guid userId);
}
