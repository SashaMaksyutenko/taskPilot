using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Taskpilot.API.Configuration;
using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>Unit tests for <see cref="GifClient"/> (disabled behaviour + Giphy parsing).</summary>
public class GifClientTests
{
    /// <summary>Returns a fixed HTTP response without any real network call.</summary>
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly string _body;
        public Uri? LastUri { get; private set; }

        public StubHandler(string body) => _body = body;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            LastUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json"),
            });
        }
    }

    private static GifClient Create(string body, GifOptions options, out StubHandler handler)
    {
        handler = new StubHandler(body);
        var http = new HttpClient(handler);
        return new GifClient(http, Options.Create(options), NullLogger<GifClient>.Instance);
    }

    [Fact]
    public async Task Disabled_WhenNoApiKey_ReturnsEmptyAndNotEnabled()
    {
        var client = Create("{}", new GifOptions { ApiKey = "" }, out _);

        Assert.False(client.IsEnabled);
        var result = await client.SearchAsync("cats", 10);
        Assert.True(result.Succeeded);
        Assert.Empty(result.Value!);
    }

    [Fact]
    public async Task Giphy_ParsesResults()
    {
        const string body = """
        {
          "data": [
            {
              "id": "abc123",
              "images": {
                "fixed_height": { "url": "https://media.giphy.com/full.gif", "width": "200", "height": "150" },
                "fixed_height_small": { "url": "https://media.giphy.com/small.gif", "width": "100", "height": "75" }
              }
            }
          ]
        }
        """;
        var client = Create(body, new GifOptions { Provider = "Giphy", ApiKey = "test-key" }, out var handler);

        var result = await client.SearchAsync("cats", 10);

        Assert.True(result.Succeeded);
        var gif = Assert.Single(result.Value!);
        Assert.Equal("abc123", gif.Id);
        Assert.Equal("https://media.giphy.com/full.gif", gif.Url);
        Assert.Equal("https://media.giphy.com/small.gif", gif.PreviewUrl);
        Assert.Equal(200, gif.Width);
        // Empty query hits the trending endpoint; a query hits search.
        Assert.Contains("/gifs/search", handler.LastUri!.ToString());
        Assert.Contains("q=cats", handler.LastUri!.ToString());
    }

    [Fact]
    public async Task Giphy_EmptyQuery_UsesTrendingEndpoint()
    {
        var client = Create("""{ "data": [] }""", new GifOptions { Provider = "Giphy", ApiKey = "k" }, out var handler);

        await client.SearchAsync(null, 10);

        Assert.Contains("/gifs/trending", handler.LastUri!.ToString());
    }
}
