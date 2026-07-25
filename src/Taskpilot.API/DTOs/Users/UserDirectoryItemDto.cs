namespace Taskpilot.API.DTOs.Users;

/// <summary>
/// A user as shown in the public members directory: only safe-to-share fields, enough to
/// browse people and open their full profile.
/// </summary>
public class UserDirectoryItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Location { get; set; }

    /// <summary>Public URL of the avatar image; null when none set.</summary>
    public string? AvatarUrl { get; set; }

    /// <summary>When the user joined.</summary>
    public DateTime MemberSince { get; set; }
}
