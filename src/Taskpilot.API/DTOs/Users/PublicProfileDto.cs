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

    /// <summary>Public URL of the avatar image; null when none set.</summary>
    public string? AvatarUrl { get; set; }

    public string? Title { get; set; }
    public string? Bio { get; set; }
    public string? Location { get; set; }

    /// <summary>Skill tags shown on the profile.</summary>
    public List<string> Skills { get; set; } = new();

    /// <summary>Per-skill endorsement counts (and whether the viewer endorsed each).</summary>
    public List<SkillEndorsementDto> SkillEndorsements { get; set; } = new();

    /// <summary>Email — only present when the user opted to show it; otherwise null.</summary>
    public string? Email { get; set; }

    // Contact / social links (shown publicly)
    public string? Website { get; set; }
    public string? LinkedIn { get; set; }
    public string? GitHub { get; set; }
    public string? Phone { get; set; }

    public DateTime MemberSince { get; set; }

    /// <summary>Average star rating from marketplace reviews (null when none yet).</summary>
    public double? AverageRating { get; set; }

    /// <summary>Number of reviews received.</summary>
    public int ReviewCount { get; set; }

    /// <summary>Computed reputation score (never negative).</summary>
    public int ReputationPoints { get; set; }

    /// <summary>Earned badge keys (the client maps them to labels/icons).</summary>
    public List<string> Badges { get; set; } = new();
}
