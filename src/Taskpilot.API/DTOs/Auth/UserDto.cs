namespace Taskpilot.API.DTOs.Auth;

/// <summary>
/// Public view of a user returned by the API (e.g. GET /api/auth/me).
/// Deliberately excludes the password hash and any other sensitive fields.
/// </summary>
public class UserDto
{
    /// <summary>User id.</summary>
    public Guid Id { get; set; }

    /// <summary>Display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Email address.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Role name (e.g. "Developer").</summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>Whether the account is active.</summary>
    public bool IsActive { get; set; }

    /// <summary>Whether two-factor authentication is enabled.</summary>
    public bool TwoFactorEnabled { get; set; }

    /// <summary>Public URL of the avatar image; null when none set.</summary>
    public string? AvatarUrl { get; set; }

    /// <summary>Job title / headline.</summary>
    public string? Title { get; set; }

    /// <summary>Short bio.</summary>
    public string? Bio { get; set; }

    /// <summary>Location.</summary>
    public string? Location { get; set; }

    /// <summary>Personal website URL.</summary>
    public string? Website { get; set; }

    /// <summary>LinkedIn profile URL.</summary>
    public string? LinkedIn { get; set; }

    /// <summary>GitHub profile URL.</summary>
    public string? GitHub { get; set; }

    /// <summary>Phone number.</summary>
    public string? Phone { get; set; }

    /// <summary>Whether the email is shown on the public profile.</summary>
    public bool ShowEmail { get; set; }

    /// <summary>UTC time the account was created.</summary>
    public DateTime CreatedAt { get; set; }
}
