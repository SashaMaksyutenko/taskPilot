namespace Taskpilot.API.DTOs.Users;

/// <summary>
/// Minimal user info returned by user search — just enough to identify and pick a
/// person (e.g. to start a chat). Excludes private fields.
/// </summary>
public class UserSearchResultDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Title { get; set; }

    /// <summary>Public URL of the avatar image; null when none set.</summary>
    public string? AvatarUrl { get; set; }
}
