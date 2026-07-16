using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Taskpilot.API.Common;
using Taskpilot.API.Configuration;

namespace Taskpilot.API.Services;

/// <summary>
/// Real chat-completions client for OpenAI. Posts the conversation to the API over
/// HTTP (Bearer key) — no SDK dependency, matching the other integration clients.
/// Disabled (returns a failure) when no API key is configured.
/// </summary>
public class OpenAiChatBotClient : IChatBotClient
{
    private readonly HttpClient _http;
    private readonly OpenAiOptions _options;
    private readonly ILogger<OpenAiChatBotClient> _logger;

    public OpenAiChatBotClient(HttpClient http, IOptions<OpenAiOptions> options, ILogger<OpenAiChatBotClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool IsEnabled => _options.IsConfigured;

    /// <inheritdoc />
    public async Task<Result<string>> CompleteAsync(IReadOnlyList<ChatBotMessage> messages)
    {
        if (!_options.IsConfigured)
            return Result<string>.Fail("The AI assistant is not configured.");

        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                model = _options.Model,
                messages = messages.Select(m => new { role = m.Role, content = m.Content }),
            });

            var endpoint = $"{_options.BaseUrl.TrimEnd('/')}/chat/completions";
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json"),
            };
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.ApiKey);

            var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("OpenAI completion failed. Status: {Status}", response.StatusCode);
                return Result<string>.Fail("The assistant could not answer right now.");
            }

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var reply = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            return string.IsNullOrWhiteSpace(reply)
                ? Result<string>.Fail("The assistant returned an empty response.")
                : Result<string>.Ok(reply.Trim());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling OpenAI.");
            return Result<string>.Fail("The assistant could not answer right now.");
        }
    }
}
