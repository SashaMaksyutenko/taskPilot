namespace Taskpilot.API.DTOs.Webhooks;

/// <summary>Input for registering an outgoing webhook. The secret is generated server-side.</summary>
public class CreateWebhookDto
{
    /// <summary>Destination URL that will receive the POST.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Event to subscribe to, e.g. "task.completed".</summary>
    public string Event { get; set; } = string.Empty;
}
