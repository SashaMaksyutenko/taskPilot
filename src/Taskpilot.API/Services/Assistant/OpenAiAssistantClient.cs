using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Taskpilot.API.Common;
using Taskpilot.API.Configuration;

namespace Taskpilot.API.Services.Assistant;

/// <summary>
/// Tool-calling chat client for OpenAI (raw HTTP, no SDK — matching the other clients).
/// Serializes the agent messages (incl. assistant tool_calls and tool results) into the
/// chat-completions format and parses either a text reply or the model's tool calls.
/// </summary>
public class OpenAiAssistantClient : IAssistantChatClient
{
    /// <summary>Total LLM call attempts (1 initial + retries) before giving up on a transient error.</summary>
    private const int MaxAttempts = 3;

    private readonly HttpClient _http;
    private readonly OpenAiOptions _options;
    private readonly ILogger<OpenAiAssistantClient> _logger;

    public OpenAiAssistantClient(HttpClient http, IOptions<OpenAiOptions> options, ILogger<OpenAiAssistantClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool IsEnabled => _options.IsConfigured;

    /// <inheritdoc />
    public async Task<Result<AssistantTurn>> CompleteAsync(
        IReadOnlyList<AgentMessage> messages, IReadOnlyList<ToolDefinition> tools)
    {
        if (!_options.IsConfigured)
            return Result<AssistantTurn>.Fail("The AI assistant is not configured.");

        try
        {
            var body = new Dictionary<string, object?>
            {
                ["model"] = _options.Model,
                ["messages"] = messages.Select(ToWire).ToList(),
            };
            if (tools.Count > 0)
            {
                body["tools"] = tools.Select(t => new
                {
                    type = "function",
                    function = new { name = t.Name, description = t.Description, parameters = t.ParametersSchema },
                });
                body["tool_choice"] = "auto";
            }

            var endpoint = $"{_options.BaseUrl.TrimEnd('/')}/chat/completions";
            var payload = JsonSerializer.Serialize(body);

            // Groq's free tier rate-limits bursts (429). Retry a couple of times with a short
            // back-off so a busy moment surfaces as a slightly slower reply, not a failure.
            for (var attempt = 1; ; attempt++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
                {
                    Content = new StringContent(payload, Encoding.UTF8, "application/json"),
                };
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.ApiKey);

                using var response = await _http.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                    var message = doc.RootElement.GetProperty("choices")[0].GetProperty("message");

                    var content = message.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String
                        ? c.GetString()
                        : null;

                    var calls = new List<ToolCall>();
                    if (message.TryGetProperty("tool_calls", out var tc) && tc.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var call in tc.EnumerateArray())
                        {
                            var fn = call.GetProperty("function");
                            calls.Add(new ToolCall(
                                call.GetProperty("id").GetString() ?? string.Empty,
                                fn.GetProperty("name").GetString() ?? string.Empty,
                                fn.TryGetProperty("arguments", out var a) ? a.GetString() ?? "{}" : "{}"));
                        }
                    }

                    return Result<AssistantTurn>.Ok(new AssistantTurn(content, calls));
                }

                // Retry only transient rate-limit / overload statuses; fail fast on anything else.
                var transient = response.StatusCode is HttpStatusCode.TooManyRequests
                    or HttpStatusCode.ServiceUnavailable or HttpStatusCode.BadGateway;
                if (!transient || attempt >= MaxAttempts)
                {
                    _logger.LogWarning("OpenAI (agent) failed. Status: {Status} (attempt {Attempt}).", response.StatusCode, attempt);
                    return Result<AssistantTurn>.Fail("The assistant could not answer right now.");
                }

                var delay = RetryDelay(response, attempt);
                _logger.LogInformation("OpenAI (agent) {Status}; retrying in {Delay}s (attempt {Attempt}/{Max}).",
                    (int)response.StatusCode, delay.TotalSeconds, attempt, MaxAttempts);
                await Task.Delay(delay);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling OpenAI (agent).");
            return Result<AssistantTurn>.Fail("The assistant could not answer right now.");
        }
    }

    /// <summary>
    /// How long to wait before retrying a transient LLM response: honours a small Retry-After
    /// header when present, else exponential back-off (1s, 2s, 4s…). Capped so a caller (a web
    /// request or the Telegram handler) never waits too long.
    /// </summary>
    private static TimeSpan RetryDelay(HttpResponseMessage response, int attempt)
    {
        var cap = TimeSpan.FromSeconds(5);
        if (response.Headers.RetryAfter?.Delta is { } delta && delta > TimeSpan.Zero)
            return delta < cap ? delta : cap;
        var backoff = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1));
        return backoff < cap ? backoff : cap;
    }

    /// <summary>Maps an agent message to the OpenAI chat-completions wire shape.</summary>
    private static object ToWire(AgentMessage m)
    {
        if (m.Role == "tool")
            return new { role = "tool", tool_call_id = m.ToolCallId, content = m.Content ?? string.Empty };

        if (m.Role == "assistant" && m.ToolCalls is { Count: > 0 })
            return new
            {
                role = "assistant",
                content = m.Content, // may be null when the assistant only calls tools
                tool_calls = m.ToolCalls.Select(t => new
                {
                    id = t.Id,
                    type = "function",
                    function = new { name = t.Name, arguments = t.Arguments },
                }),
            };

        return new { role = m.Role, content = m.Content ?? string.Empty };
    }
}
