using Microsoft.Extensions.Logging.Abstractions;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Projects;
using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>Tests for per-column WIP limits: set/upsert, clear, access guard and validation.</summary>
public class WipLimitServiceTests
{
    private static WipLimitService Make(TaskpilotDbContext ctx) =>
        new(ctx, NullLogger<WipLimitService>.Instance);

    [Fact]
    public async Task Set_CreatesLimit_ThenGetReturnsIt()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var project = await TestDb.AddProjectAsync(ctx, owner, "P");
        var svc = Make(ctx);

        var set = await svc.SetAsync(owner, project, new SetWipLimitDto { Status = "InProgress", MaxTasks = 3 });
        Assert.True(set.Succeeded);

        var limits = (await svc.GetAsync(owner, project)).Value!;
        var limit = Assert.Single(limits);
        Assert.Equal("InProgress", limit.Status);
        Assert.Equal(3, limit.MaxTasks);
    }

    [Fact]
    public async Task Set_UpsertsExisting_AndClearsWithNull()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var project = await TestDb.AddProjectAsync(ctx, owner, "P");
        var svc = Make(ctx);

        await svc.SetAsync(owner, project, new SetWipLimitDto { Status = "Review", MaxTasks = 2 });
        // Setting again updates the same row (no duplicate).
        var updated = (await svc.SetAsync(owner, project, new SetWipLimitDto { Status = "Review", MaxTasks = 5 })).Value!;
        Assert.Equal(5, Assert.Single(updated).MaxTasks);

        // A null limit clears it.
        var cleared = (await svc.SetAsync(owner, project, new SetWipLimitDto { Status = "Review", MaxTasks = null })).Value!;
        Assert.Empty(cleared);
    }

    [Fact]
    public async Task Set_WithoutWriteAccess_Fails()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var stranger = await TestDb.AddUserAsync(ctx, "Stranger");
        var project = await TestDb.AddProjectAsync(ctx, owner, "P");

        Assert.False((await Make(ctx).SetAsync(stranger, project, new SetWipLimitDto { Status = "Backlog", MaxTasks = 1 })).Succeeded);
    }

    [Fact]
    public async Task Set_InvalidStatus_Fails()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var project = await TestDb.AddProjectAsync(ctx, owner, "P");

        Assert.False((await Make(ctx).SetAsync(owner, project, new SetWipLimitDto { Status = "Nonsense", MaxTasks = 1 })).Succeeded);
    }
}
