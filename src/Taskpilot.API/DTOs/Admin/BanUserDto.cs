namespace Taskpilot.API.DTOs.Admin;

/// <summary>Options for banning a user.</summary>
public class BanUserDto
{
    /// <summary>Ban length in days; null or 0 means a permanent ban.</summary>
    public int? Days { get; set; }
}
