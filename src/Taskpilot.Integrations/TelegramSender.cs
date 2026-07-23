using Microsoft.Extensions.Options;

namespace Taskpilot.Integrations;

/// <summary>
/// Sends Telegram messages via the Bot API over HTTP. Disabled (no-op) when no bot
/// token is configured, so the app runs fine without Telegram set up.
/// </summary>
public class TelegramSender : ITelegramSender
{
    private readonly HttpClient _http;
    private readonly TelegramOptions _options;
    private readonly ILogger<TelegramSender> _logger;

    public TelegramSender(HttpClient http, IOptions<TelegramOptions> options, ILogger<TelegramSender> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool IsEnabled => _options.IsConfigured;

    /// <inheritdoc />
    public async Task SendMessageAsync(string chatId, string text)
    {
        if (!IsEnabled)
            return; // bot not configured — do nothing

        try
        {
            var url = $"https://api.telegram.org/bot{_options.BotToken}/sendMessage";
            var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["chat_id"] = chatId,
                ["text"] = text,
                ["disable_web_page_preview"] = "true",
            });

            var response = await _http.PostAsync(url, form);
            if (!response.IsSuccessStatusCode)
                _logger.LogWarning("Telegram sendMessage returned {Status} for chat {ChatId}.", response.StatusCode, chatId);
        }
        catch (Exception ex)
        {
            // Best-effort — never let a delivery failure break the caller.
            _logger.LogError(ex, "Failed to send Telegram message to chat {ChatId}.", chatId);
        }
    }
}
