namespace Taskpilot.API.DTOs.Admin;

/// <summary>Result of issuing a warning, so the admin UI can react to escalation.</summary>
public class IssueWarningResultDto
{
    /// <summary>The warning that was just created.</summary>
    public WarningDto Warning { get; set; } = new();

    /// <summary>Total number of warnings the user now has.</summary>
    public int WarningCount { get; set; }

    /// <summary>True when this warning pushed the user over the threshold and auto-banned them.</summary>
    public bool AutoBanned { get; set; }
}
