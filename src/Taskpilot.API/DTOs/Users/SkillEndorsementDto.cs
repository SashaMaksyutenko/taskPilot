namespace Taskpilot.API.DTOs.Users;

/// <summary>One of a profile's skills together with how many colleagues have endorsed it.</summary>
public class SkillEndorsementDto
{
    /// <summary>The skill name (matches an entry in the profile's Skills list).</summary>
    public string Skill { get; set; } = string.Empty;

    /// <summary>Number of colleagues who have endorsed this skill.</summary>
    public int Count { get; set; }

    /// <summary>Whether the current viewer has endorsed this skill.</summary>
    public bool EndorsedByViewer { get; set; }
}

/// <summary>Result of toggling an endorsement on one of a user's skills.</summary>
public class SkillEndorsementResultDto
{
    /// <summary>The skill that was endorsed or un-endorsed.</summary>
    public string Skill { get; set; } = string.Empty;

    /// <summary>True if the skill is now endorsed by the caller, false if the endorsement was removed.</summary>
    public bool Endorsed { get; set; }

    /// <summary>The skill's total endorsement count after the change.</summary>
    public int Count { get; set; }
}

/// <summary>Request body for endorsing (toggling) a skill on a user's profile.</summary>
public class EndorseSkillDto
{
    /// <summary>The skill to endorse; must be one the target user lists.</summary>
    public string Skill { get; set; } = string.Empty;
}
