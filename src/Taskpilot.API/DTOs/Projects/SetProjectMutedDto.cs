namespace Taskpilot.API.DTOs.Projects;

/// <summary>Input for muting or unmuting a project's notifications.</summary>
public class SetProjectMutedDto
{
    /// <summary>True to mute the project, false to unmute it.</summary>
    public bool Muted { get; set; }
}
