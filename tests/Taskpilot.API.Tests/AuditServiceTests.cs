using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>Unit tests for <see cref="AuditService"/> over an in-memory database.</summary>
public class AuditServiceTests
{
    private static AuditService Create(Taskpilot.API.Data.TaskpilotDbContext ctx) =>
        new(ctx, NullLogger<AuditService>.Instance);

    [Fact]
    public async Task LogAsync_PersistsEntryWithAllFields()
    {
        using var ctx = TestDb.CreateContext();
        var svc = Create(ctx);
        var actorId = Guid.NewGuid();

        await svc.LogAsync(
            action: "auth.login.success",
            actorId: actorId,
            actorEmail: "alice@test.local",
            entityType: "User",
            entityId: actorId.ToString(),
            details: "logged in",
            ipAddress: "127.0.0.1");

        var entry = await ctx.AuditLogs.SingleAsync();
        Assert.Equal("auth.login.success", entry.Action);
        Assert.Equal(actorId, entry.ActorId);
        Assert.Equal("alice@test.local", entry.ActorEmail);
        Assert.Equal("User", entry.EntityType);
        Assert.Equal("logged in", entry.Details);
        Assert.Equal("127.0.0.1", entry.IpAddress);
        Assert.NotEqual(default, entry.CreatedAt);
    }

    [Fact]
    public async Task LogAsync_AllowsSystemActionWithoutActor()
    {
        using var ctx = TestDb.CreateContext();
        var svc = Create(ctx);

        // A system/anonymous action has no actor id or email.
        await svc.LogAsync("system.startup.seed");

        var entry = await ctx.AuditLogs.SingleAsync();
        Assert.Equal("system.startup.seed", entry.Action);
        Assert.Null(entry.ActorId);
        Assert.Null(entry.ActorEmail);
    }

    [Fact]
    public async Task GetAsync_ReturnsNewestFirstAndPages()
    {
        using var ctx = TestDb.CreateContext();
        var svc = Create(ctx);
        // Three entries written in order; CreatedAt is set on write.
        await svc.LogAsync("a.one");
        await Task.Delay(5);
        await svc.LogAsync("a.two");
        await Task.Delay(5);
        await svc.LogAsync("a.three");

        var firstPage = await svc.GetAsync(page: 1, pageSize: 2);

        Assert.True(firstPage.Succeeded);
        Assert.Equal(3, firstPage.Value!.Total);          // total across all pages
        Assert.Equal(2, firstPage.Value.Items.Count);     // page size respected
        Assert.Equal("a.three", firstPage.Value.Items[0].Action); // newest first
        Assert.Equal("a.two", firstPage.Value.Items[1].Action);

        var secondPage = await svc.GetAsync(page: 2, pageSize: 2);
        Assert.Single(secondPage.Value!.Items);
        Assert.Equal("a.one", secondPage.Value.Items[0].Action);
    }

    [Fact]
    public async Task GetAsync_FiltersByAction()
    {
        using var ctx = TestDb.CreateContext();
        var svc = Create(ctx);
        await svc.LogAsync("auth.login.success");
        await svc.LogAsync("auth.login.failed");
        await svc.LogAsync("auth.login.failed");

        var failed = await svc.GetAsync(page: 1, pageSize: 50, action: "auth.login.failed");

        Assert.Equal(2, failed.Value!.Total);
        Assert.All(failed.Value.Items, i => Assert.Equal("auth.login.failed", i.Action));
    }
}
