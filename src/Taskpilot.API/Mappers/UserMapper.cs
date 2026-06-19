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
}
