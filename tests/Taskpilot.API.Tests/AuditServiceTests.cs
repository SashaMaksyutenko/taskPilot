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
}
