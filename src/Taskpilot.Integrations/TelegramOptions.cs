namespace Taskpilot.Integrations;

/// <summary>
/// Telegram bot settings, bound from configuration (section "Telegram").
/// The token comes from @BotFather — keep it out of source control.
/// </summary>
public class TelegramOptions
{
    /// <summary>Bot token from @BotFather. Empty disables the bot and Telegram delivery.</summary>
    public string BotToken { get; set; } = string.Empty;

    /// <summary>Bot username (without @), shown to users so they know which bot to open.</summary>
    public string BotUsername { get; set; } = string.Empty;

    /// <summary>
    /// Shared secret Telegram echoes back in the "X-Telegram-Bot-Api-Secret-Token" header of every
    /// webhook call (set when registering the webhook). When non-empty the webhook rejects any
    /// request without a matching header, so only Telegram can reach the update handler.
    /// </summary>
    public string WebhookSecret { get; set; } = string.Empty;

    /// <summary>True only when a bot token is configured.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(BotToken);
}
