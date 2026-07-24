namespace Taskpilot.API.DTOs.Chat;

/// <summary>A user the current user has blocked from direct messaging.</summary>
public class BlockedUserDto
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>Public URL of the blocked user's avatar; null when none set.</summary>
    public string? AvatarUrl { get; set; }

    /// <summary>UTC time the block was created.</summary>
    public DateTime BlockedAt { get; set; }
}
