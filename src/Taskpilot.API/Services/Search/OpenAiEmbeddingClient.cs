using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Taskpilot.API.Common;
using Taskpilot.API.Configuration;

namespace Taskpilot.API.Services.Search;

/// <summary>
/// Embedding client for the OpenAI-compatible `/embeddings` endpoint (raw HTTP, no SDK —
/// matching the assistant client). Disabled until an API key is configured.
/// </summary>
public class OpenAiEmbeddingClient : IEmbeddingClient
{
    private readonly HttpClient _http;
    private readonly EmbeddingOptions _options;
    private readonly ILogger<OpenAiEmbeddingClient> _logger;

    public OpenAiEmbeddingClient(HttpClient http, IOptions<EmbeddingOptions> options, ILogger<OpenAiEmbeddingClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool IsEnabled => _options.IsConfigured;

    /// <inheritdoc />
    public async Task<Result<List<float[]>>> EmbedAsync(IReadOnlyList<string> inputs, CancellationToken ct = default)
    {
        if (!_options.IsConfigured)
            return Result<List<float[]>>.Fail("Embeddings are not configured.");
        if (inputs.Count == 0)
            return Result<List<float[]>>.Ok(new List<float[]>());

        try
        {
            var body = JsonSerializer.Serialize(new { model = _options.Model, input = inputs });
            var endpoint = $"{_options.BaseUrl.TrimEnd('/')}/embeddings";

            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

            using var response = await _http.SendAsync(request, ct);
            var json = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Embeddings request failed: {Status}", response.StatusCode);
                return Result<List<float[]>>.Fail("The embeddings provider returned an error.");
            }

            using var doc = JsonDocument.Parse(json);
            // Response order matches the input order, but honour the explicit index to be safe.
            var data = doc.RootElement.GetProperty("data").EnumerateArray()
                .Select(e => new
                {
                    Index = e.GetProperty("index").GetInt32(),
                    Vector = e.GetProperty("embedding").EnumerateArray().Select(x => (float)x.GetDouble()).ToArray(),
                })
                .OrderBy(x => x.Index)
                .Select(x => x.Vector)
                .ToList();

            return Result<List<float[]>>.Ok(data);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Embeddings request threw.");
            return Result<List<float[]>>.Fail("The embeddings provider is unavailable.");
        }
    }
}
