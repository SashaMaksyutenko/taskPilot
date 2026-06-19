namespace Taskpilot.API.DTOs.Users;

/// <summary>Input for updating the current user's profile.</summary>
public class UpdateProfileDto
{
    public string Name { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Bio { get; set; }
    public string? Location { get; set; }
}
