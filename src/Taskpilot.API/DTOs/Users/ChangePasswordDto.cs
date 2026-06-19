namespace Taskpilot.API.DTOs.Users;

/// <summary>Input for changing the current user's password.</summary>
public class ChangePasswordDto
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}
