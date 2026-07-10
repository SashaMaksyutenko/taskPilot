namespace Taskpilot.API.DTOs.Forum;

/// <summary>A boolean toggle payload (used for pin/lock topic endpoints).</summary>
public class SetFlagDto
{
    public bool Value { get; set; }
}
