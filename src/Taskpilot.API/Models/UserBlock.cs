namespace Taskpilot.API.Models;

/// <summary>
/// One user blocking another in direct messaging: while a block exists in either
/// direction between two users, neither can send the other a direct message or open a
/// new direct conversation. Blocker/blocked are stored by id (no navigation) to keep the
/// model simple and avoid extra cascade paths to Users, mirroring <see cref="Review"/>.
/// </summary>
public class UserBlock
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>The user who created the block.</summary>
    public Guid BlockerId { get; set; }

    /// <summary>The user who is blocked.</summary>
    public Guid BlockedId { get; set; }

    /// <summary>UTC time the block was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
