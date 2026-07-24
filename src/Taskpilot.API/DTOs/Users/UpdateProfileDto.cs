namespace Taskpilot.API.DTOs.Users;

/// <summary>Input for updating the current user's profile.</summary>
public class UpdateProfileDto
{
    public string Name { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Bio { get; set; }
    public string? Location { get; set; }

    /// <summary>Skill tags (e.g. ["C#", "React"]); normalized and de-duplicated on save.</summary>
    public List<string> Skills { get; set; } = new();

    // Contact / social links
    public string? Website { get; set; }
    public string? LinkedIn { get; set; }
    public string? GitHub { get; set; }
    public string? Phone { get; set; }

    /// <summary>Whether to show the email on the public profile.</summary>
    public bool ShowEmail { get; set; }
}
