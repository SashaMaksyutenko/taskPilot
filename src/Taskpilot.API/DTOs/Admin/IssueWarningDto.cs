namespace Taskpilot.API.DTOs.Admin;

/// <summary>Payload to issue a moderation warning to a user.</summary>
public class IssueWarningDto
{
    public string Reason { get; set; } = string.Empty;
}
