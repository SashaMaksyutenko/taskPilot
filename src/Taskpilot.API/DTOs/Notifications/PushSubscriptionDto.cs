namespace Taskpilot.API.DTOs.Notifications;

/// <summary>Browser push subscription sent by the frontend after PushManager.subscribe().</summary>
public class PushSubscriptionDto
{
    /// <summary>Push service endpoint URL.</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>Client public key (p256dh).</summary>
    public string P256dh { get; set; } = string.Empty;

    /// <summary>Client auth secret.</summary>
    public string Auth { get; set; } = string.Empty;
}

/// <summary>Endpoint to remove a browser push subscription.</summary>
public class PushUnsubscribeDto
{
    /// <summary>Push service endpoint URL to remove.</summary>
    public string Endpoint { get; set; } = string.Empty;
}
