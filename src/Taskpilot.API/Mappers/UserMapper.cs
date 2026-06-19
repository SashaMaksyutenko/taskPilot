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
    /// <summary>Full self-view (used by /me and profile update). Includes email.</summary>
    public static UserDto ToDto(User u) => new()
    {
        Id = u.Id,
        Name = u.Name,
        Email = u.Email,
        Role = u.Role.ToString(),
        IsActive = u.IsActive,
        Title = u.Title,
        Bio = u.Bio,
        Location = u.Location,
        CreatedAt = u.CreatedAt,
    };

    /// <summary>Public view of another user. Excludes email and account status.</summary>
    public static PublicProfileDto ToPublicProfile(User u) => new()
    {
        Id = u.Id,
        Name = u.Name,
        Role = u.Role.ToString(),
        Title = u.Title,
        Bio = u.Bio,
        Location = u.Location,
        MemberSince = u.CreatedAt,
    };
}
