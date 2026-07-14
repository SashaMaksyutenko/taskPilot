using System.Text.Json;
using Microsoft.Extensions.Options;
using Taskpilot.API.Configuration;
using Taskpilot.API.Services;

namespace Taskpilot.API.Workers;

/// <summary>
/// Background worker that long-polls the Telegram Bot API for incoming messages and
/// handles the "/start &lt;code&gt;" linking command and "/help". Does nothing when no
/// bot token is configured.
/// </summary>
public class TelegramPollingService : BackgroundService
{
    // Back-off between failed polls: starts short, doubles up to a ceiling, and resets
    // after the first success. Keeps an unreachable Telegram from hammering the network
    // (and flooding the log) every few seconds.
    private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpFactory;
    private readonly TelegramOptions _options;
    private readonly ILogger<TelegramPollingService> _logger;

    public TelegramPollingService(
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpFactory,
        IOptions<TelegramOptions> options,
        ILogger<TelegramPollingService> logger)
    {
        _scopeFactory = scopeFactory;
        _httpFactory = httpFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.IsConfigured)
        {
            _logger.LogInformation("Telegram bot not configured; polling disabled.");
            return;
        }

        _logger.LogInformation("Telegram polling started.");
        var http = _httpFactory.CreateClient();
        http.Timeout = TimeSpan.FromSeconds(35); // longer than the long-poll timeout
        long offset = 0;
        var retryDelay = InitialRetryDelay;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var url = $"https://api.telegram.org/bot{_options.BotToken}/getUpdates?timeout=25&offset={offset}";
                var response = await http.GetAsync(url, stoppingToken);
                if (!response.IsSuccessStatusCode)
                {
                    // Telegram answered but rejected the call (bad token, rate limit…).
                    _logger.LogWarning("Telegram getUpdates returned {Status}; retrying in {Delay}.",
                        (int)response.StatusCode, retryDelay);
                    retryDelay = await BackOffAsync(retryDelay, stoppingToken);
                    continue;
                }

                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(stoppingToken));

                // The call worked, so drop back to the short delay.
                retryDelay = InitialRetryDelay;

                if (!doc.RootElement.TryGetProperty("result", out var updates))
                    continue;

                foreach (var update in updates.EnumerateArray())
                {
                    offset = update.GetProperty("update_id").GetInt64() + 1; // ack this update
                    await HandleUpdateAsync(update);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
            {
                // Telegram is unreachable (blocked, offline, timed out). Expected in some
                // networks, so log one line without the stack trace and back off.
                _logger.LogWarning("Telegram unreachable ({Reason}); retrying in {Delay}.",
                    ex.Message, retryDelay);
                retryDelay = await BackOffAsync(retryDelay, stoppingToken);
            }
            catch (Exception ex)
            {
                // Anything else is a real bug — keep the full stack trace.
                _logger.LogError(ex, "Telegram polling error.");
                retryDelay = await BackOffAsync(retryDelay, stoppingToken);
            }
        }
    }

    /// <summary>Waits out the current delay, then returns the next (doubled, capped) one.</summary>
    private static async Task<TimeSpan> BackOffAsync(TimeSpan delay, CancellationToken stoppingToken)
    {
        await Task.Delay(delay, stoppingToken);
        var next = delay * 2;
        return next > MaxRetryDelay ? MaxRetryDelay : next;
    }

    /// <summary>Handles one update: links accounts on "/start &lt;code&gt;", replies to "/help".</summary>
    private async Task HandleUpdateAsync(JsonElement update)
    {
        if (!update.TryGetProperty("message", out var message))
            return;
        if (!message.TryGetProperty("text", out var textEl) || !message.TryGetProperty("chat", out var chat))
            return;

        var chatId = chat.GetProperty("id").GetRawText(); // numeric id as string
        var text = textEl.GetString()?.Trim() ?? string.Empty;

        using var scope = _scopeFactory.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ITelegramSender>();

        if (text.StartsWith("/help", StringComparison.OrdinalIgnoreCase))
        {
            await sender.SendMessageAsync(chatId, "TaskPilot bot — link your account in the app's Settings to receive notifications here. Send the code shown there (or use \"/start <code>\").");
            return;
        }

        // "/start" with no code -> greeting; otherwise treat the last token (from
        // "/start <code>" or a plain "<code>" message) as a link code.
        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var isBareStart = parts.Length == 1 && parts[0].Equals("/start", StringComparison.OrdinalIgnoreCase);
        if (isBareStart)
        {
            await sender.SendMessageAsync(chatId, "Welcome to TaskPilot! Open Settings in the app, tap \"Connect Telegram\", and send me the code shown there.");
            return;
        }

        // The code is the last token (handles both "/start CODE" and just "CODE").
        var code = parts[^1];
        var links = scope.ServiceProvider.GetRequiredService<ITelegramLinkService>();
        var linked = await links.LinkByCodeAsync(code, chatId);
        await sender.SendMessageAsync(chatId, linked
            ? "✅ Your Telegram is now linked to TaskPilot. You'll get notifications here."
            : "That link code is invalid or expired. Generate a new one in TaskPilot settings.");
    }
}
