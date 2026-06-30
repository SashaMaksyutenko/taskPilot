using Taskpilot.API.DTOs.Admin;
using Taskpilot.API.DTOs.Auth;
using Taskpilot.API.DTOs.Users;
using Taskpilot.API.Models;

namespace Taskpilot.API.Mappers;

/// <summary>
/// Maps a <see cref="User"/> entity to its DTOs.
/// Centralised so every place that returns a user is consistent and never leaks the hash.
/// </summary>
public static class UserMapper
{
    /// <summary>
    /// Public URL for a user's avatar image, or null when none is set. The
    /// <c>v</c> query string busts the browser cache when the avatar changes.
    /// </summary>
    public static string? AvatarUrl(Guid userId, Guid? avatarFileId) =>
        avatarFileId is { } fileId ? $"/api/users/{userId}/avatar?v={fileId:N}" : null;

    /// <summary>Convenience overload for a loaded <see cref="User"/> entity.</summary>
    public static string? AvatarUrl(User u) => AvatarUrl(u.Id, u.AvatarFileId);

    /// <summary>Full self-view (used by /me and profile update). Includes email.</summary>
    public static UserDto ToDto(User u) => new()
    {
        Id = u.Id,
        Name = u.Name,
        Email = u.Email,
        Role = u.Role.ToString(),
        IsActive = u.IsActive,
        AvatarUrl = AvatarUrl(u),
        Title = u.Title,
        Bio = u.Bio,
        Location = u.Location,
        Website = u.Website,
        LinkedIn = u.LinkedIn,
        GitHub = u.GitHub,
        Phone = u.Phone,
        ShowEmail = u.ShowEmail,
        CreatedAt = u.CreatedAt,
    };

    /// <summary>
    /// Public view of another user. Account status is never exposed; the email is
    /// only included when the user opted in via <see cref="User.ShowEmail"/>.
    /// </summary>
    public static PublicProfileDto ToPublicProfile(User u) => new()
    {
        Id = u.Id,
        Name = u.Name,
        Role = u.Role.ToString(),
        AvatarUrl = AvatarUrl(u),
        Title = u.Title,
        Bio = u.Bio,
        Location = u.Location,
        Email = u.ShowEmail ? u.Email : null,
        Website = u.Website,
        LinkedIn = u.LinkedIn,
        GitHub = u.GitHub,
        Phone = u.Phone,
        MemberSince = u.CreatedAt,
    };

    /// <summary>Admin view of a user (full management row).</summary>
    public static AdminUserDto ToAdminDto(User u) => new()
    {
        Id = u.Id,
        Name = u.Name,
        AvatarUrl = AvatarUrl(u),
        Email = u.Email,
        Role = u.Role.ToString(),
        IsActive = u.IsActive,
        BannedUntil = u.BannedUntil,
        CreatedAt = u.CreatedAt,
    };
}
