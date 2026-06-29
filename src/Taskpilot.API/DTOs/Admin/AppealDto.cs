namespace Taskpilot.API.DTOs.Admin;

/// <summary>A moderation appeal as returned to clients.</summary>
public class AppealDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    /// <summary>Display name of the appealing user.</summary>
    public string UserName { get; set; } = string.Empty;

    public Guid? WarningId { get; set; }

    /// <summary>Reason of the appealed warning, when still linked.</summary>
    public string? WarningReason { get; set; }

    public string Message { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ReviewNote { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
}
