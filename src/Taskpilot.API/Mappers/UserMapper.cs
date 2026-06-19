using Taskpilot.API.DTOs.Auth;
using Taskpilot.API.Models;

namespace Taskpilot.API.Mappers;

/// <summary>
/// Maps a <see cref="User"/> entity to its public <see cref="UserDto"/>.
/// Centralised so every place that returns a user is consistent and never leaks the hash.
/// </summary>
public static class UserMapper
{
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
}
