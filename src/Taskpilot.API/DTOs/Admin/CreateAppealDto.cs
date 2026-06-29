namespace Taskpilot.API.DTOs.Admin;

/// <summary>Payload for a user to file an appeal against a warning.</summary>
public class CreateAppealDto
{
    /// <summary>Optional warning being appealed.</summary>
    public Guid? WarningId { get; set; }

    /// <summary>The user's explanation.</summary>
    public string Message { get; set; } = string.Empty;
}
