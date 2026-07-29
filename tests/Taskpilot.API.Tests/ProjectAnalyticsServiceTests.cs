using Taskpilot.API.Data;
using Taskpilot.API.Models;
using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>Tests for project delivery analytics (status/priority mix, weekly trend, cycle time, workload).</summary>
public class ProjectAnalyticsServiceTests
{
    private static ProjectAnalyticsService Make(TaskpilotDbContext ctx) => new(ctx);

    private static void AddTask(TaskpilotDbContext ctx, Guid projectId, Guid creator,
        ProjectTaskStatus status, TaskPriority priority, DateTime createdAt, DateTime? completedAt = null, Guid? assigneeId = null)
    {
        ctx.ProjectTasks.Add(new ProjectTask
        {
            Id = Guid.NewGuid(), ProjectId = projectId, CreatorId = creator, Title = "T",
            Status = status, Priority = priority, CreatedAt = createdAt, CompletedAt = completedAt, AssigneeId = assigneeId,
        });
    }

    [Fact]
    public async Task Analytics_ComputesTotalsStatusAndPriority()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var project = await TestDb.AddProjectAsync(ctx, owner, "P");
        var now = DateTime.UtcNow;
        AddTask(ctx, project, owner, ProjectTaskStatus.Backlog, TaskPriority.High, now);
        AddTask(ctx, project, owner, ProjectTaskStatus.Backlog, TaskPriority.High, now);
        AddTask(ctx, project, owner, ProjectTaskStatus.Done, TaskPriority.Low, now, now);
        await ctx.SaveChangesAsync();

        var a = (await Make(ctx).GetAnalyticsAsync(owner, project)).Value!;

        Assert.Equal(3, a.TotalTasks);
        Assert.Equal(2, a.ByStatus["Backlog"]);
        Assert.Equal(1, a.ByStatus["Done"]);
        Assert.Equal(0, a.ByStatus["InProgress"]); // every column present
        Assert.Equal(2, a.ByPriority["High"]);
        Assert.Equal(1, a.ByPriority["Low"]);
    }

    [Fact]
    public async Task Analytics_ComputesCycleTimeAndThroughput()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var project = await TestDb.AddProjectAsync(ctx, owner, "P");
        var now = DateTime.UtcNow;
        // Completed this week, ~5 days after creation.
        AddTask(ctx, project, owner, ProjectTaskStatus.Done, TaskPriority.Medium, now.AddDays(-5), now);
        await ctx.SaveChangesAsync();

        var a = (await Make(ctx).GetAnalyticsAsync(owner, project)).Value!;

        Assert.NotNull(a.AvgCycleTimeDays);
        Assert.InRange(a.AvgCycleTimeDays!.Value, 4.5, 5.5);
        Assert.Equal(8, a.Weeks.Count);
        Assert.True(a.ThroughputThisWeek >= 1);
    }

    [Fact]
    public async Task Analytics_ByAssignee_GroupsOpenAndDone()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var alice = await TestDb.AddUserAsync(ctx, "Alice");
        var project = await TestDb.AddProjectAsync(ctx, owner, "P");
        var now = DateTime.UtcNow;
        AddTask(ctx, project, owner, ProjectTaskStatus.InProgress, TaskPriority.Medium, now, null, alice);
        AddTask(ctx, project, owner, ProjectTaskStatus.Done, TaskPriority.Medium, now, now, alice);
        AddTask(ctx, project, owner, ProjectTaskStatus.Backlog, TaskPriority.Medium, now); // unassigned
        await ctx.SaveChangesAsync();

        var a = (await Make(ctx).GetAnalyticsAsync(owner, project)).Value!;

        var aliceLoad = a.ByAssignee.Single(x => x.Name == "Alice");
        Assert.Equal(1, aliceLoad.Open);
        Assert.Equal(1, aliceLoad.Done);
        Assert.Contains(a.ByAssignee, x => x.Name == "Unassigned" && x.Open == 1);
    }

    [Fact]
    public async Task Analytics_ByNonMember_Fails()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var stranger = await TestDb.AddUserAsync(ctx, "Stranger");
        var project = await TestDb.AddProjectAsync(ctx, owner, "P");

        Assert.False((await Make(ctx).GetAnalyticsAsync(stranger, project)).Succeeded);
    }
}
