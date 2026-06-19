namespace Taskpilot.API.DTOs.Admin;

/// <summary>Input for changing a user's role (Developer/Manager/Admin/Viewer).</summary>
public class ChangeRoleDto
{
    public string Role { get; set; } = string.Empty;
}
