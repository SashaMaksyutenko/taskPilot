namespace Taskpilot.API.DTOs.Users;

/// <summary>
/// Public view of another user's profile. Excludes private fields (email, account
/// status) — only what is safe to show to other users.
/// </summary>
public class PublicProfileDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Bio { get; set; }
    public string? Location { get; set; }
    public DateTime MemberSince { get; set; }
}
