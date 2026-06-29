namespace Taskpilot.API.DTOs.Admin;

/// <summary>Admin decision on an appeal.</summary>
public class ResolveAppealDto
{
    /// <summary>True to approve (removes the linked warning); false to reject.</summary>
    public bool Approve { get; set; }

    /// <summary>Optional note explaining the decision.</summary>
    public string? Note { get; set; }
}
