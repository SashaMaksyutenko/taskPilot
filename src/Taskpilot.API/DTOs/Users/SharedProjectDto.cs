namespace Taskpilot.API.DTOs.Users;

/// <summary>
/// A project shown on a user's profile as shared participation: the profile user and the
/// viewer both take part in it. (On your own profile these are simply all your projects.)
/// Names of private projects the viewer has no access to are never exposed.
/// </summary>
public class SharedProjectDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Color { get; set; }
}
