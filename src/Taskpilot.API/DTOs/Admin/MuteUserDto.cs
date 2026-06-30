namespace Taskpilot.API.DTOs.Admin;

/// <summary>Options for muting a user.</summary>
public class MuteUserDto
{
    /// <summary>Mute length in days; defaults to 1 when null or not positive.</summary>
    public int? Days { get; set; }
}
