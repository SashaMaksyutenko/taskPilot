using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Webhooks;
using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>
/// Unit tests for webhook delivery: retries on failure, the delivery log,
/// pause/resume and the test-webhook action.
/// </summary>
public class WebhookDeliveryTests
{
    private static WebhookService Create(TaskpilotDbContext ctx, ScriptedHandler handler)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>()))
               .Returns(() => new HttpClient(handler, disposeHandler: false));
        return new WebhookService(ctx, factory.Object, NullLogger<WebhookService>.Instance);
    }

    private static CreateWebhookDto Dto() =>
        new() { Url = "https://receiver.test/hook", Event = "task.created" };

    [Fact]
    public async Task Delivery_Success_LogsOneAttempt()
    {
        using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var handler = new ScriptedHandler(HttpStatusCode.OK);
        var svc = Create(ctx, handler);
        await svc.CreateAsync(owner, Dto());

        await svc.DispatchAsync("task.created", new { ok = true });

        var delivery = await ctx.WebhookDeliveries.SingleAsync();
        Assert.True(delivery.Success);
        Assert.Equal(200, delivery.StatusCode);
        Assert.Equal(1, delivery.Attempts);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task Delivery_TransientFailure_RetriesThenSucceeds()
    {
        using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        // Fails the first two attempts, then answers 200.
        var handler = new ScriptedHandler(HttpStatusCode.OK, throwFirst: 2);
        var svc = Create(ctx, handler);
        await svc.CreateAsync(owner, Dto());

        await svc.DispatchAsync("task.created", new { ok = true });

        var delivery = await ctx.WebhookDeliveries.SingleAsync();
        Assert.True(delivery.Success);
        Assert.Equal(3, delivery.Attempts);
        Assert.Equal(3, handler.CallCount);
    }

    [Fact]
    public async Task Delivery_AlwaysFailing_StopsAtThreeAttempts()
    {
        using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var handler = new ScriptedHandler(HttpStatusCode.OK, throwFirst: 99); // never succeeds
        var svc = Create(ctx, handler);
        await svc.CreateAsync(owner, Dto());

        await svc.DispatchAsync("task.created", new { ok = true });

        var delivery = await ctx.WebhookDeliveries.SingleAsync();
        Assert.False(delivery.Success);
        Assert.Equal(3, delivery.Attempts);
        Assert.NotNull(delivery.Error);
        Assert.Equal(3, handler.CallCount);
    }

    [Fact]
    public async Task Delivery_NonSuccessStatus_IsRetriedAndRecorded()
    {
        using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var handler = new ScriptedHandler(HttpStatusCode.InternalServerError);
        var svc = Create(ctx, handler);
        await svc.CreateAsync(owner, Dto());

        await svc.DispatchAsync("task.created", new { ok = true });

        var delivery = await ctx.WebhookDeliveries.SingleAsync();
        Assert.False(delivery.Success);
        Assert.Equal(500, delivery.StatusCode);
        Assert.Equal(3, delivery.Attempts); // a 5xx is retried like a transport error
    }

    [Fact]
    public async Task Pause_StopsDelivery_ResumeRestoresIt()
    {
        using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var handler = new ScriptedHandler(HttpStatusCode.OK);
        var svc = Create(ctx, handler);
        var hook = (await svc.CreateAsync(owner, Dto())).Value!;

        await svc.SetActiveAsync(owner, hook.Id, isActive: false);
        await svc.DispatchAsync("task.created", new { ok = true });
        Assert.Equal(0, handler.CallCount);

        await svc.SetActiveAsync(owner, hook.Id, isActive: true);
        await svc.DispatchAsync("task.created", new { ok = true });
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task Test_SendsSamplePayload_EvenWhenPaused()
    {
        using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var handler = new ScriptedHandler(HttpStatusCode.OK);
        var svc = Create(ctx, handler);
        var hook = (await svc.CreateAsync(owner, Dto())).Value!;
        await svc.SetActiveAsync(owner, hook.Id, isActive: false);

        var result = await svc.TestAsync(owner, hook.Id);

        Assert.True(result.Succeeded);
        Assert.True(result.Value!.Success);
        Assert.Equal(1, handler.CallCount);
        Assert.Contains("\"test\":true", handler.LastBody);
    }

    [Fact]
    public async Task GetDeliveries_OnlyForTheOwner()
    {
        using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var other = await TestDb.AddUserAsync(ctx, "Other");
        var handler = new ScriptedHandler(HttpStatusCode.OK);
        var svc = Create(ctx, handler);
        var hook = (await svc.CreateAsync(owner, Dto())).Value!;
        await svc.DispatchAsync("task.created", new { ok = true });

        Assert.Single((await svc.GetDeliveriesAsync(owner, hook.Id)).Value!);
        Assert.False((await svc.GetDeliveriesAsync(other, hook.Id)).Succeeded);
    }

    /// <summary>Handler that throws for the first N calls, then returns a fixed status.</summary>
    private sealed class ScriptedHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly int _throwFirst;

        public int CallCount { get; private set; }
        public string LastBody { get; private set; } = string.Empty;

        public ScriptedHandler(HttpStatusCode status, int throwFirst = 0)
        {
            _status = status;
            _throwFirst = throwFirst;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            LastBody = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            if (CallCount <= _throwFirst)
                throw new HttpRequestException("simulated failure");

            return new HttpResponseMessage(_status);
        }
    }
}
