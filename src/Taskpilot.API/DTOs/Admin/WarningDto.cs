namespace Taskpilot.API.DTOs.Admin;

/// <summary>A moderation warning as returned to clients.</summary>
public class WarningDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Reason { get; set; } = string.Empty;

    /// <summary>Display name of the admin who issued the warning.</summary>
    public string IssuedByName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}
