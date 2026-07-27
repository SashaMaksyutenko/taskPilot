using System.Text.Json;
using Microsoft.Extensions.Options;
using Taskpilot.API.DTOs.Integrations;
using Taskpilot.API.Services;
using Taskpilot.Integrations;

namespace Taskpilot.API.Workers;

/// <summary>
/// Background worker that long-polls the Telegram Bot API for incoming messages and hands each
/// off to <see cref="ITelegramUpdateService"/> (linking commands plus, for a linked user, the AI
/// assistant). Does nothing when no bot token is configured. Long-polling is used instead of a
/// webhook so the bot works without a fixed public HTTPS URL — do NOT also register a webhook,
/// as Telegram allows only one delivery method at a time.
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

    /// <summary>Deserialises one raw update and delegates to the shared update handler.</summary>
    private async Task HandleUpdateAsync(JsonElement update)
    {
        var parsed = update.Deserialize<TelegramUpdate>();
        if (parsed?.Message is null)
            return;

        using var scope = _scopeFactory.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<ITelegramUpdateService>();
        await handler.HandleUpdateAsync(parsed);
    }
}
