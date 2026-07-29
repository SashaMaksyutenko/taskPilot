using Microsoft.Extensions.Logging.Abstractions;
using Taskpilot.API.Data;
using Taskpilot.API.Models;
using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>Tests for task dependencies: same-project/self/duplicate guards, cycle detection and the blocked graph.</summary>
public class TaskDependencyServiceTests
{
    private static TaskDependencyService Make(TaskpilotDbContext ctx) =>
        new(ctx, NullLogger<TaskDependencyService>.Instance);

    private static async Task<Guid> SeedTaskAsync(TaskpilotDbContext ctx, Guid owner, Guid projectId,
        string title = "T", ProjectTaskStatus status = ProjectTaskStatus.Backlog)
    {
        var id = Guid.NewGuid();
        ctx.ProjectTasks.Add(new ProjectTask { Id = id, ProjectId = projectId, CreatorId = owner, Title = title, Status = status });
        await ctx.SaveChangesAsync();
        return id;
    }

    [Fact]
    public async Task Add_CreatesEdge_AndReportsBothDirections()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var project = await TestDb.AddProjectAsync(ctx, owner, "P");
        var a = await SeedTaskAsync(ctx, owner, project, "A");
        var b = await SeedTaskAsync(ctx, owner, project, "B");

        var svc = Make(ctx);
        var added = await svc.AddAsync(owner, a, b); // A depends on B
        Assert.True(added.Succeeded);
        Assert.Contains(added.Value!.DependsOn, r => r.Id == b);

        var bGraph = (await svc.GetAsync(owner, b)).Value!;
        Assert.Contains(bGraph.Blocks, r => r.Id == a); // B blocks A
    }

    [Fact]
    public async Task Add_SelfDependency_Fails()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var project = await TestDb.AddProjectAsync(ctx, owner, "P");
        var a = await SeedTaskAsync(ctx, owner, project, "A");

        Assert.False((await Make(ctx).AddAsync(owner, a, a)).Succeeded);
    }

    [Fact]
    public async Task Add_CrossProject_Fails()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var p1 = await TestDb.AddProjectAsync(ctx, owner, "P1");
        var p2 = await TestDb.AddProjectAsync(ctx, owner, "P2");
        var a = await SeedTaskAsync(ctx, owner, p1, "A");
        var b = await SeedTaskAsync(ctx, owner, p2, "B");

        Assert.False((await Make(ctx).AddAsync(owner, a, b)).Succeeded);
    }

    [Fact]
    public async Task Add_Duplicate_Fails()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var project = await TestDb.AddProjectAsync(ctx, owner, "P");
        var a = await SeedTaskAsync(ctx, owner, project, "A");
        var b = await SeedTaskAsync(ctx, owner, project, "B");
        var svc = Make(ctx);

        Assert.True((await svc.AddAsync(owner, a, b)).Succeeded);
        Assert.False((await svc.AddAsync(owner, a, b)).Succeeded);
    }

    [Fact]
    public async Task Add_DirectCycle_Fails()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var project = await TestDb.AddProjectAsync(ctx, owner, "P");
        var a = await SeedTaskAsync(ctx, owner, project, "A");
        var b = await SeedTaskAsync(ctx, owner, project, "B");
        var svc = Make(ctx);

        Assert.True((await svc.AddAsync(owner, a, b)).Succeeded); // A → B
        Assert.False((await svc.AddAsync(owner, b, a)).Succeeded); // B → A would cycle
    }

    [Fact]
    public async Task Add_IndirectCycle_Fails()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var project = await TestDb.AddProjectAsync(ctx, owner, "P");
        var a = await SeedTaskAsync(ctx, owner, project, "A");
        var b = await SeedTaskAsync(ctx, owner, project, "B");
        var c = await SeedTaskAsync(ctx, owner, project, "C");
        var svc = Make(ctx);

        Assert.True((await svc.AddAsync(owner, a, b)).Succeeded); // A → B
        Assert.True((await svc.AddAsync(owner, b, c)).Succeeded); // B → C
        Assert.False((await svc.AddAsync(owner, c, a)).Succeeded); // C → A would cycle (A→B→C→A)
    }

    [Fact]
    public async Task IsBlocked_TrueUntilBlockerIsDone()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var project = await TestDb.AddProjectAsync(ctx, owner, "P");
        var a = await SeedTaskAsync(ctx, owner, project, "A");
        var b = await SeedTaskAsync(ctx, owner, project, "B", ProjectTaskStatus.Backlog);
        var svc = Make(ctx);
        await svc.AddAsync(owner, a, b);

        Assert.True((await svc.GetAsync(owner, a)).Value!.IsBlocked);

        (await ctx.ProjectTasks.FindAsync(b))!.Status = ProjectTaskStatus.Done;
        await ctx.SaveChangesAsync();

        Assert.False((await svc.GetAsync(owner, a)).Value!.IsBlocked);
    }

    [Fact]
    public async Task Add_ByNonMember_Fails()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var outsider = await TestDb.AddUserAsync(ctx, "Outsider");
        var project = await TestDb.AddProjectAsync(ctx, owner, "P");
        var a = await SeedTaskAsync(ctx, owner, project, "A");
        var b = await SeedTaskAsync(ctx, owner, project, "B");

        Assert.False((await Make(ctx).AddAsync(outsider, a, b)).Succeeded);
    }

    [Fact]
    public async Task Remove_DeletesEdge()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var project = await TestDb.AddProjectAsync(ctx, owner, "P");
        var a = await SeedTaskAsync(ctx, owner, project, "A");
        var b = await SeedTaskAsync(ctx, owner, project, "B");
        var svc = Make(ctx);
        await svc.AddAsync(owner, a, b);

        Assert.True((await svc.RemoveAsync(owner, a, b)).Succeeded);
        Assert.Empty((await svc.GetAsync(owner, a)).Value!.DependsOn);
    }
}
