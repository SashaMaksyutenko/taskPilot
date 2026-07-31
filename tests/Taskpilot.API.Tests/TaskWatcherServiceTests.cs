using Microsoft.Extensions.Logging.Abstractions;
using Taskpilot.API.Data;
using Taskpilot.API.Models;
using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>Tests for task watchers: opt-in/idempotent subscribe, unsubscribe, access guard and listing.</summary>
public class TaskWatcherServiceTests
{
    private static TaskWatcherService Make(TaskpilotDbContext ctx) =>
        new(ctx, NullLogger<TaskWatcherService>.Instance);

    private static async Task<Guid> SeedTaskAsync(TaskpilotDbContext ctx, Guid owner, Guid projectId, string title = "T")
    {
        var id = Guid.NewGuid();
        ctx.ProjectTasks.Add(new ProjectTask { Id = id, ProjectId = projectId, CreatorId = owner, Title = title });
        await ctx.SaveChangesAsync();
        return id;
    }

    [Fact]
    public async Task Watch_AddsCaller_AndIsIdempotent()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var project = await TestDb.AddProjectAsync(ctx, owner, "P");
        var task = await SeedTaskAsync(ctx, owner, project);
        var svc = Make(ctx);

        var first = await svc.WatchAsync(owner, task);
        Assert.True(first.Succeeded);
        Assert.True(first.Value!.IsWatching);
        Assert.Single(first.Value!.Watchers);

        // Watching again is a no-op — still exactly one watcher.
        var again = await svc.WatchAsync(owner, task);
        Assert.Single(again.Value!.Watchers);
    }

    [Fact]
    public async Task Unwatch_RemovesCaller()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var project = await TestDb.AddProjectAsync(ctx, owner, "P");
        var task = await SeedTaskAsync(ctx, owner, project);
        var svc = Make(ctx);

        await svc.WatchAsync(owner, task);
        var after = await svc.UnwatchAsync(owner, task);

        Assert.True(after.Succeeded);
        Assert.False(after.Value!.IsWatching);
        Assert.Empty(after.Value!.Watchers);
    }

    [Fact]
    public async Task Watch_WithoutProjectAccess_Fails()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var stranger = await TestDb.AddUserAsync(ctx, "Stranger");
        var project = await TestDb.AddProjectAsync(ctx, owner, "P");
        var task = await SeedTaskAsync(ctx, owner, project);

        var result = await Make(ctx).WatchAsync(stranger, task);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Get_ListsWatchers_AndIsWatchingReflectsCaller()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var member = await TestDb.AddUserAsync(ctx, "Member");
        var project = await TestDb.AddProjectAsync(ctx, owner, "P");
        ctx.ProjectMembers.Add(new ProjectMember { Id = Guid.NewGuid(), ProjectId = project, UserId = member });
        await ctx.SaveChangesAsync();
        var task = await SeedTaskAsync(ctx, owner, project);
        var svc = Make(ctx);

        await svc.WatchAsync(member, task);

        // The member is watching; the owner is not (but can see the list).
        var forOwner = (await svc.GetAsync(owner, task)).Value!;
        Assert.Single(forOwner.Watchers);
        Assert.Equal(member, forOwner.Watchers[0].UserId);
        Assert.False(forOwner.IsWatching);

        var forMember = (await svc.GetAsync(member, task)).Value!;
        Assert.True(forMember.IsWatching);
    }
}
