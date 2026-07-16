using System.Text.Json;
using Microsoft.Extensions.Options;
using Taskpilot.API.Common;
using Taskpilot.API.Configuration;
using Taskpilot.API.DTOs.Gif;

namespace Taskpilot.API.Services;

/// <summary>
/// Real GIF search client. Proxies to Giphy or Tenor over HTTP (key stays server-side,
/// never exposed to the browser) — no SDK, matching the other integration clients.
/// Disabled (returns an empty list) when no API key is configured.
/// </summary>
public class GifClient : IGifClient
{
    private readonly HttpClient _http;
    private readonly GifOptions _options;
    private readonly ILogger<GifClient> _logger;

    public GifClient(HttpClient http, IOptions<GifOptions> options, ILogger<GifClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool IsEnabled => _options.IsConfigured;

    /// <inheritdoc />
    public async Task<Result<List<GifDto>>> SearchAsync(string? query, int limit)
    {
        if (!_options.IsConfigured)
            return Result<List<GifDto>>.Ok(new List<GifDto>());

        // Clamp the page size to a sane range.
        if (limit < 1) limit = 1;
        if (limit > 50) limit = 50;

        try
        {
            var isTenor = string.Equals(_options.Provider, "Tenor", StringComparison.OrdinalIgnoreCase);
            var url = isTenor ? BuildTenorUrl(query, limit) : BuildGiphyUrl(query, limit);

            var response = await _http.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("GIF search failed. Provider: {Provider}, Status: {Status}", _options.Provider, response.StatusCode);
                return Result<List<GifDto>>.Fail("GIF search is unavailable right now.");
            }

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var gifs = isTenor ? ParseTenor(doc) : ParseGiphy(doc);
            return Result<List<GifDto>>.Ok(gifs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching GIFs. Provider: {Provider}", _options.Provider);
            return Result<List<GifDto>>.Fail("GIF search is unavailable right now.");
        }
    }

    // --- Giphy ---

    private string BuildGiphyUrl(string? query, int limit)
    {
        var q = Uri.EscapeDataString(query?.Trim() ?? string.Empty);
        var key = Uri.EscapeDataString(_options.ApiKey);
        var rating = Uri.EscapeDataString(_options.Rating);
        // Empty query -> trending endpoint; otherwise search.
        return string.IsNullOrWhiteSpace(query)
            ? $"https://api.giphy.com/v1/gifs/trending?api_key={key}&limit={limit}&rating={rating}"
            : $"https://api.giphy.com/v1/gifs/search?api_key={key}&q={q}&limit={limit}&rating={rating}";
    }

    private static List<GifDto> ParseGiphy(JsonDocument doc)
    {
        var list = new List<GifDto>();
        if (!doc.RootElement.TryGetProperty("data", out var data)) return list;
        foreach (var item in data.EnumerateArray())
        {
            var images = item.GetProperty("images");
            var full = images.GetProperty("fixed_height");
            // A smaller still/animated preview for the grid, falling back to the full gif.
            var preview = images.TryGetProperty("fixed_height_small", out var small) ? small : full;
            list.Add(new GifDto
            {
                Id = item.GetProperty("id").GetString() ?? string.Empty,
                Url = full.GetProperty("url").GetString() ?? string.Empty,
                PreviewUrl = preview.GetProperty("url").GetString() ?? string.Empty,
                Width = int.TryParse(full.GetProperty("width").GetString(), out var w) ? w : 0,
                Height = int.TryParse(full.GetProperty("height").GetString(), out var h) ? h : 0,
            });
        }
        return list;
    }

    // --- Tenor (v2) ---

    private string BuildTenorUrl(string? query, int limit)
    {
        var key = Uri.EscapeDataString(_options.ApiKey);
        var common = $"key={key}&limit={limit}&media_filter=gif,tinygif&contentfilter=medium";
        return string.IsNullOrWhiteSpace(query)
            ? $"https://tenor.googleapis.com/v2/featured?{common}"
            : $"https://tenor.googleapis.com/v2/search?q={Uri.EscapeDataString(query.Trim())}&{common}";
    }

    private static List<GifDto> ParseTenor(JsonDocument doc)
    {
        var list = new List<GifDto>();
        if (!doc.RootElement.TryGetProperty("results", out var results)) return list;
        foreach (var item in results.EnumerateArray())
        {
            var formats = item.GetProperty("media_formats");
            var gif = formats.GetProperty("gif");
            var preview = formats.TryGetProperty("tinygif", out var tiny) ? tiny : gif;
            var dims = gif.TryGetProperty("dims", out var d) && d.GetArrayLength() >= 2
                ? (d[0].GetInt32(), d[1].GetInt32())
                : (0, 0);
            list.Add(new GifDto
            {
                Id = item.GetProperty("id").GetString() ?? string.Empty,
                Url = gif.GetProperty("url").GetString() ?? string.Empty,
                PreviewUrl = preview.GetProperty("url").GetString() ?? string.Empty,
                Width = dims.Item1,
                Height = dims.Item2,
            });
        }
        return list;
    }
}
