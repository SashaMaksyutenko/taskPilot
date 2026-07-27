using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Taskpilot.API.Configuration;
using Taskpilot.API.Services.Assistant;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>
/// Tests the assistant's LLM HTTP client — specifically that a transient 429 (Groq's free-tier
/// rate limit) is retried rather than surfaced as a failure, while real errors fail fast.
/// </summary>
public class OpenAiAssistantClientTests
{
    /// <summary>An HttpMessageHandler that returns a queued sequence of responses and counts calls.</summary>
    private sealed class SequenceHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses;
        public int Calls { get; private set; }

        public SequenceHandler(params HttpResponseMessage[] responses) => _responses = new Queue<HttpResponseMessage>(responses);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(_responses.Dequeue());
        }
    }

    private static HttpResponseMessage Ok(string content) =>
        new(HttpStatusCode.OK) { Content = new StringContent(content) };

    private static OpenAiAssistantClient Client(SequenceHandler handler) =>
        new(new HttpClient(handler),
            Options.Create(new OpenAiOptions { ApiKey = "k", Model = "m", BaseUrl = "https://llm.test/v1" }),
            NullLogger<OpenAiAssistantClient>.Instance);

    [Fact]
    public async Task Retries_On429_ThenSucceeds()
    {
        var handler = new SequenceHandler(
            new HttpResponseMessage(HttpStatusCode.TooManyRequests),
            Ok("{\"choices\":[{\"message\":{\"content\":\"hello\"}}]}"));

        var result = await Client(handler).CompleteAsync(
            new[] { AgentMessage.User("hi") }, Array.Empty<ToolDefinition>());

        Assert.True(result.Succeeded);
        Assert.Equal("hello", result.Value!.Content);
        Assert.Equal(2, handler.Calls); // retried once after the 429
    }

    [Fact]
    public async Task FailsFast_OnNonTransientStatus()
    {
        var handler = new SequenceHandler(new HttpResponseMessage(HttpStatusCode.BadRequest));

        var result = await Client(handler).CompleteAsync(
            new[] { AgentMessage.User("hi") }, Array.Empty<ToolDefinition>());

        Assert.False(result.Succeeded);
        Assert.Equal(1, handler.Calls); // a 400 is not retried
    }

    [Fact]
    public async Task GivesUp_AfterMaxAttempts_WhenAlwaysRateLimited()
    {
        var handler = new SequenceHandler(
            new HttpResponseMessage(HttpStatusCode.TooManyRequests),
            new HttpResponseMessage(HttpStatusCode.TooManyRequests),
            new HttpResponseMessage(HttpStatusCode.TooManyRequests));

        var result = await Client(handler).CompleteAsync(
            new[] { AgentMessage.User("hi") }, Array.Empty<ToolDefinition>());

        Assert.False(result.Succeeded);
        Assert.Equal(3, handler.Calls); // MaxAttempts
    }
}
