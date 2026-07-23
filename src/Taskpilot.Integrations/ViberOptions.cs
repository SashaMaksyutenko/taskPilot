namespace Taskpilot.Integrations;

/// <summary>
/// Viber bot settings, bound from configuration (section "Viber"). The auth token
/// comes from the Viber admin panel — keep it out of source control. Empty token
/// disables Viber linking and delivery.
/// </summary>
public class ViberOptions
{
    /// <summary>Auth token from the Viber bot account. Empty disables Viber.</summary>
    public string AuthToken { get; set; } = string.Empty;

    /// <summary>Sender name shown on messages the bot sends (required by Viber).</summary>
    public string BotName { get; set; } = "TaskPilot";

    /// <summary>True only when an auth token is configured.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(AuthToken);
}
