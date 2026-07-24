namespace Taskpilot.API.Models;

/// <summary>
/// One colleague vouching for one of a user's listed skills. Endorsed user and endorser
/// are stored by id (no navigation) to keep the model simple and avoid extra cascade paths
/// to Users, mirroring <see cref="Review"/>. A unique index on (UserId, Skill, EndorserId)
/// allows at most one endorsement per endorser per skill.
/// </summary>
public class SkillEndorsement
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>The user whose skill is endorsed.</summary>
    public Guid UserId { get; set; }

    /// <summary>The skill being endorsed (stored with the profile's canonical spelling).</summary>
    public string Skill { get; set; } = string.Empty;

    /// <summary>The colleague giving the endorsement.</summary>
    public Guid EndorserId { get; set; }

    /// <summary>UTC time the endorsement was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
