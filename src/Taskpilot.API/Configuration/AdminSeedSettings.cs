namespace Taskpilot.API.Configuration;

/// <summary>
/// Credentials for the initial admin user, bound from the "Admin" config section
/// (populated from .env: Admin__Email, Admin__Password, Admin__Name).
/// On startup the app ensures this user exists with the Admin role.
/// </summary>
public class AdminSeedSettings
{
    public string? Email { get; set; }
    public string? Password { get; set; }
    public string? Name { get; set; }
}
