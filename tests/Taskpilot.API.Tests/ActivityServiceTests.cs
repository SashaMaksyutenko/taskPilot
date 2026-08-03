using Taskpilot.API.Data;
using Taskpilot.API.Models;
using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>Tests for the project activity feed: task audit entries, ordering, scoping and access.</summary>
public class ActivityServiceTests
{
    private static ActivityService Make(TaskpilotDbContext ctx) => new(ctx);

    private static async Task<Guid> SeedTaskAsync(TaskpilotDbContext ctx, Guid owner, Guid projectId)
    {
        var id = Guid.NewGuid();
        ctx.ProjectTasks.Add(new ProjectTask { Id = id, ProjectId = projectId, CreatorId = owner, Title = "T" });
        await ctx.SaveChangesAsync();
        return id;
    }

    private static void AddAudit(TaskpilotDbContext ctx, Guid? actor, string action, Guid taskId, DateTime when) =>
        ctx.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(), ActorId = actor, ActorEmail = "x@x", Action = action,
            EntityType = nameof(ProjectTask), EntityId = taskId.ToString(), Details = "d", CreatedAt = when,
        });

    [Fact]
    public async Task GetActivity_ReturnsTaskEvents_NewestFirst_WithActorName()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var project = await TestDb.AddProjectAsync(ctx, owner, "P");
        var task = await SeedTaskAsync(ctx, owner, project);
        var now = DateTime.UtcNow;
        AddAudit(ctx, owner, "task.created", task, now.AddMinutes(-2));
        AddAudit(ctx, owner, "task.status.changed", task, now.AddMinutes(-1));
        await ctx.SaveChangesAsync();

        var feed = (await Make(ctx).GetProjectActivityAsync(owner, project)).Value!;

        Assert.Equal(2, feed.Count);
        Assert.Equal("task.status.changed", feed[0].Action); // newest first
        Assert.Equal("Owner", feed[0].ActorName);
        Assert.Equal(task, feed[0].TaskId);
    }

    [Fact]
    public async Task GetActivity_IsScopedToTheProject()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var a = await TestDb.AddProjectAsync(ctx, owner, "A");
        var b = await TestDb.AddProjectAsync(ctx, owner, "B");
        var taskA = await SeedTaskAsync(ctx, owner, a);
        var taskB = await SeedTaskAsync(ctx, owner, b);
        AddAudit(ctx, owner, "task.created", taskA, DateTime.UtcNow);
        AddAudit(ctx, owner, "task.created", taskB, DateTime.UtcNow);
        await ctx.SaveChangesAsync();

        var feed = (await Make(ctx).GetProjectActivityAsync(owner, a)).Value!;

        Assert.Single(feed);
        Assert.Equal(taskA, feed[0].TaskId);
    }

    [Fact]
    public async Task GetActivity_ShowsDeletedUser_ForUnknownActor()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var project = await TestDb.AddProjectAsync(ctx, owner, "P");
        var task = await SeedTaskAsync(ctx, owner, project);
        AddAudit(ctx, Guid.NewGuid(), "task.deleted", task, DateTime.UtcNow); // actor no longer exists
        await ctx.SaveChangesAsync();

        var feed = (await Make(ctx).GetProjectActivityAsync(owner, project)).Value!;

        Assert.Equal("Deleted user", Assert.Single(feed).ActorName);
    }

    [Fact]
    public async Task GetActivity_WithoutAccess_Fails()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var stranger = await TestDb.AddUserAsync(ctx, "Stranger");
        var project = await TestDb.AddProjectAsync(ctx, owner, "P");

        Assert.False((await Make(ctx).GetProjectActivityAsync(stranger, project)).Succeeded);
    }
}
