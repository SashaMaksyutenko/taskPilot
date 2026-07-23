using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Taskpilot.Integrations;

/// <summary>
/// Sends Viber messages via the Bot API over HTTP. Disabled (no-op) when no auth
/// token is configured, so the app runs fine without Viber set up.
/// </summary>
public class ViberSender : IViberSender
{
    private const string SendEndpoint = "https://chatapi.viber.com/pa/send_message";

    private readonly HttpClient _http;
    private readonly ViberOptions _options;
    private readonly ILogger<ViberSender> _logger;

    public ViberSender(HttpClient http, IOptions<ViberOptions> options, ILogger<ViberSender> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool IsEnabled => _options.IsConfigured;

    /// <inheritdoc />
    public async Task SendMessageAsync(string receiverId, string text)
    {
        if (!IsEnabled)
            return; // bot not configured — do nothing

        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                receiver = receiverId,
                min_api_version = 1,
                sender = new { name = _options.BotName },
                type = "text",
                text,
            });

            using var request = new HttpRequestMessage(HttpMethod.Post, SendEndpoint)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json"),
            };
            request.Headers.Add("X-Viber-Auth-Token", _options.AuthToken);

            var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                _logger.LogWarning("Viber send_message returned {Status} for receiver {ReceiverId}.", response.StatusCode, receiverId);
        }
        catch (Exception ex)
        {
            // Best-effort — never let a delivery failure break the caller.
            _logger.LogError(ex, "Failed to send Viber message to receiver {ReceiverId}.", receiverId);
        }
    }
}
