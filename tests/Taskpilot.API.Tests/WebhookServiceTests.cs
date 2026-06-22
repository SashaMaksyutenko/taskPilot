using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Taskpilot.API.DTOs.Webhooks;
using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>Unit tests for <see cref="WebhookService"/> over an in-memory database.</summary>
public class WebhookServiceTests
{
    /// <summary>Builds a service whose outgoing HTTP requests are captured by <paramref name="handler"/>.</summary>
    private static WebhookService Create(Taskpilot.API.Data.TaskpilotDbContext ctx, RecordingHandler? handler = null)
    {
        handler ??= new RecordingHandler();
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(handler, disposeHandler: false));
        return new WebhookService(ctx, factory.Object, NullLogger<WebhookService>.Instance);
    }

    [Fact]
    public async Task CreateAsync_StoresWebhookWithGeneratedSecret()
    {
        using var ctx = TestDb.CreateContext();
        var ownerId = await TestDb.AddUserAsync(ctx, "Alice");
        var svc = Create(ctx);

        var result = await svc.CreateAsync(ownerId, new CreateWebhookDto { Url = "https://x.com/h", Event = "task.created" });

        Assert.True(result.Succeeded);
        Assert.Equal("task.created", result.Value!.Event);
        Assert.True(result.Value.IsActive);
        // 32 random bytes encoded as lowercase hex => 64 characters.
        Assert.Equal(64, result.Value.Secret.Length);
        Assert.Equal(result.Value.Secret, result.Value.Secret.ToLowerInvariant());
        Assert.Equal(1, await ctx.Webhooks.CountAsync());
    }

    [Fact]
    public async Task GetForUserAsync_ReturnsOnlyOwnersWebhooks()
    {
        using var ctx = TestDb.CreateContext();
        var alice = await TestDb.AddUserAsync(ctx, "Alice");
        var bob = await TestDb.AddUserAsync(ctx, "Bob");
        var svc = Create(ctx);
        await svc.CreateAsync(alice, new CreateWebhookDto { Url = "https://a.com", Event = "task.created" });
        await svc.CreateAsync(alice, new CreateWebhookDto { Url = "https://a2.com", Event = "project.created" });
        await svc.CreateAsync(bob, new CreateWebhookDto { Url = "https://b.com", Event = "task.created" });

        var result = await svc.GetForUserAsync(alice);

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.Value!.Count);
        Assert.All(result.Value, w => Assert.StartsWith("https://a", w.Url));
    }

    [Fact]
    public async Task DeleteAsync_OtherUser_NotFoundAndKeepsWebhook()
    {
        using var ctx = TestDb.CreateContext();
        var alice = await TestDb.AddUserAsync(ctx, "Alice");
        var bob = await TestDb.AddUserAsync(ctx, "Bob");
        var svc = Create(ctx);
        var created = await svc.CreateAsync(alice, new CreateWebhookDto { Url = "https://a.com", Event = "task.created" });

        var result = await svc.DeleteAsync(bob, created.Value!.Id);

        Assert.False(result.Succeeded);
        Assert.Equal("Webhook not found.", result.Error);
        Assert.Equal(1, await ctx.Webhooks.CountAsync());
    }

    [Fact]
    public async Task DeleteAsync_Owner_RemovesWebhook()
    {
        using var ctx = TestDb.CreateContext();
        var alice = await TestDb.AddUserAsync(ctx, "Alice");
        var svc = Create(ctx);
        var created = await svc.CreateAsync(alice, new CreateWebhookDto { Url = "https://a.com", Event = "task.created" });

        var result = await svc.DeleteAsync(alice, created.Value!.Id);

        Assert.True(result.Succeeded);
        Assert.Equal(0, await ctx.Webhooks.CountAsync());
    }

    [Fact]
    public async Task DispatchAsync_SendsSignedPostToMatchingActiveHook()
    {
        using var ctx = TestDb.CreateContext();
        var alice = await TestDb.AddUserAsync(ctx, "Alice");
        var handler = new RecordingHandler();
        var svc = Create(ctx, handler);
        var created = await svc.CreateAsync(alice, new CreateWebhookDto { Url = "https://a.com/hook", Event = "task.created" });
        var secret = created.Value!.Secret;

        await svc.DispatchAsync("task.created", new { taskId = Guid.NewGuid(), title = "Hi" });

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.Equal("https://a.com/hook", req.Url);
        Assert.Equal("task.created", req.Event);
        // The signature header must equal HMAC-SHA256(secret, body).
        Assert.Equal(ExpectedSignature(secret, req.Body), req.Signature);
    }

    [Fact]
    public async Task DispatchAsync_SkipsInactiveAndNonMatchingHooks()
    {
        using var ctx = TestDb.CreateContext();
        var alice = await TestDb.AddUserAsync(ctx, "Alice");
        var handler = new RecordingHandler();
        var svc = Create(ctx, handler);

        // Different event -> should be skipped.
        await svc.CreateAsync(alice, new CreateWebhookDto { Url = "https://other.com", Event = "project.created" });
        // Matching event but deactivated -> should be skipped.
        var inactive = await svc.CreateAsync(alice, new CreateWebhookDto { Url = "https://off.com", Event = "task.created" });
        var entity = await ctx.Webhooks.FirstAsync(w => w.Id == inactive.Value!.Id);
        entity.IsActive = false;
        await ctx.SaveChangesAsync();

        await svc.DispatchAsync("task.created", new { ok = true });

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task DispatchAsync_OneFailingReceiver_DoesNotThrowAndStillCallsOthers()
    {
        using var ctx = TestDb.CreateContext();
        var alice = await TestDb.AddUserAsync(ctx, "Alice");
        // The handler throws for the "bad" host but succeeds otherwise.
        var handler = new RecordingHandler(throwForUrl: "https://bad.com/hook");
        var svc = Create(ctx, handler);
        await svc.CreateAsync(alice, new CreateWebhookDto { Url = "https://bad.com/hook", Event = "task.created" });
        await svc.CreateAsync(alice, new CreateWebhookDto { Url = "https://good.com/hook", Event = "task.created" });

        // Must complete without throwing despite the failing receiver.
        await svc.DispatchAsync("task.created", new { ok = true });

        var req = Assert.Single(handler.Requests); // only the good one recorded
        Assert.Equal("https://good.com/hook", req.Url);
    }

    private static string ExpectedSignature(string secret, string payload)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>Captured details of one outgoing webhook request.</summary>
    private sealed record CapturedRequest(HttpMethod Method, string Url, string Event, string Signature, string Body);

    /// <summary>Test handler that records requests and optionally fails for a given URL.</summary>
    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly string? _throwForUrl;
        public List<CapturedRequest> Requests { get; } = new();

        public RecordingHandler(string? throwForUrl = null) => _throwForUrl = throwForUrl;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.ToString();
            if (_throwForUrl is not null && url == _throwForUrl)
                throw new HttpRequestException("simulated failure");

            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            request.Headers.TryGetValues("X-Taskpilot-Event", out var ev);
            request.Headers.TryGetValues("X-Taskpilot-Signature", out var sig);
            Requests.Add(new CapturedRequest(
                request.Method,
                url,
                ev?.FirstOrDefault() ?? string.Empty,
                sig?.FirstOrDefault() ?? string.Empty,
                body));

            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
