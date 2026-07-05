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

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var url = $"https://api.telegram.org/bot{_options.BotToken}/getUpdates?timeout=25&offset={offset}";
                var response = await http.GetAsync(url, stoppingToken);
                if (!response.IsSuccessStatusCode)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                    continue;
                }

                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(stoppingToken));
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Telegram polling error.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
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

        if (text.StartsWith("/start", StringComparison.OrdinalIgnoreCase))
        {
            var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                var links = scope.ServiceProvider.GetRequiredService<ITelegramLinkService>();
                var linked = await links.LinkByCodeAsync(parts[1], chatId);
                await sender.SendMessageAsync(chatId, linked
                    ? "✅ Your Telegram is now linked to TaskPilot. You'll get notifications here."
                    : "That link code is invalid or expired. Generate a new one in TaskPilot settings.");
            }
            else
            {
                await sender.SendMessageAsync(chatId, "Welcome to TaskPilot! Open Settings in the app and use \"Connect Telegram\" to link your account.");
            }
        }
        else if (text.StartsWith("/help", StringComparison.OrdinalIgnoreCase))
        {
            await sender.SendMessageAsync(chatId, "TaskPilot bot — link your account in the app's Settings to receive notifications here. Command: /start <code>.");
        }
    }
}
