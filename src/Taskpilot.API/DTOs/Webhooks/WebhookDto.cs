namespace Taskpilot.API.DTOs.Webhooks;

/// <summary>
/// A webhook as returned to the owner. The secret is included so the owner can
/// configure their receiver to verify the HMAC signature.
/// </summary>
public class WebhookDto
{
    public Guid Id { get; set; }
    public string Url { get; set; } = string.Empty;
    public string Event { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}
