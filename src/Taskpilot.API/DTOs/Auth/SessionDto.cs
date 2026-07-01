namespace Taskpilot.API.DTOs.Auth;

/// <summary>An active login session (a non-revoked, non-expired refresh token).</summary>
public class SessionDto
{
    public Guid Id { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }

    /// <summary>True for the session that made this request (not revocable from the UI).</summary>
    public bool IsCurrent { get; set; }
}
