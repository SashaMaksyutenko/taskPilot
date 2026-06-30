namespace Taskpilot.API.DTOs.Admin;

/// <summary>Full user row for the admin user-management table.</summary>
public class AdminUserDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }

    /// <summary>When a temporary ban lifts; null for active or permanently banned users.</summary>
    public DateTime? BannedUntil { get; set; }

    /// <summary>When a mute lifts; null when not muted.</summary>
    public DateTime? MutedUntil { get; set; }

    public DateTime CreatedAt { get; set; }
}
