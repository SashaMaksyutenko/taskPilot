using Microsoft.Extensions.Logging.Abstractions;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Projects;
using Taskpilot.API.Models;
using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>Tests for sprints: CRUD, status, moving tasks in/out and task tallies.</summary>
public class SprintServiceTests
{
    private static SprintService Make(TaskpilotDbContext ctx) => new(ctx, NullLogger<SprintService>.Instance);

    private static async Task<Guid> SeedTaskAsync(TaskpilotDbContext ctx, Guid owner, Guid projectId,
        ProjectTaskStatus status = ProjectTaskStatus.Backlog)
    {
        var id = Guid.NewGuid();
        ctx.ProjectTasks.Add(new ProjectTask { Id = id, ProjectId = projectId, CreatorId = owner, Title = "T", Status = status });
        await ctx.SaveChangesAsync();
        return id;
    }

    [Fact]
    public async Task Create_ByOwner_StartsPlanned()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var project = await TestDb.AddProjectAsync(ctx, owner, "P");

        var result = await Make(ctx).CreateSprintAsync(owner, project, new SaveSprintDto { Name = "Sprint 1", Goal = "Ship v1" });

        Assert.True(result.Succeeded);
        Assert.Equal("Sprint 1", result.Value!.Name);
        Assert.Equal("Planned", result.Value.Status);
        Assert.Equal(0, result.Value.TaskCount);
    }

    [Fact]
    public async Task Create_ByNonMember_Fails()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var stranger = await TestDb.AddUserAsync(ctx, "Stranger");
        var project = await TestDb.AddProjectAsync(ctx, owner, "P");

        Assert.False((await Make(ctx).CreateSprintAsync(stranger, project, new SaveSprintDto { Name = "S" })).Succeeded);
    }

    [Fact]
    public async Task AssignTask_MovesIntoSprint_AndTalliesReflectDone()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var project = await TestDb.AddProjectAsync(ctx, owner, "P");
        var svc = Make(ctx);
        var sprint = (await svc.CreateSprintAsync(owner, project, new SaveSprintDto { Name = "S1" })).Value!;
        var task = await SeedTaskAsync(ctx, owner, project);

        Assert.True((await svc.AssignTaskAsync(owner, task, sprint.Id)).Succeeded);

        var afterAssign = (await svc.GetSprintsAsync(owner, project)).Value!.Single();
        Assert.Equal(1, afterAssign.TaskCount);
        Assert.Equal(0, afterAssign.DoneCount);

        // Finish the task, tallies update.
        (await ctx.ProjectTasks.FindAsync(task))!.Status = ProjectTaskStatus.Done;
        await ctx.SaveChangesAsync();

        var afterDone = (await svc.GetSprintsAsync(owner, project)).Value!.Single();
        Assert.Equal(1, afterDone.TaskCount);
        Assert.Equal(1, afterDone.DoneCount);
    }

    [Fact]
    public async Task AssignTask_ToSprintInAnotherProject_Fails()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var p1 = await TestDb.AddProjectAsync(ctx, owner, "P1");
        var p2 = await TestDb.AddProjectAsync(ctx, owner, "P2");
        var svc = Make(ctx);
        var sprintInP2 = (await svc.CreateSprintAsync(owner, p2, new SaveSprintDto { Name = "S" })).Value!;
        var taskInP1 = await SeedTaskAsync(ctx, owner, p1);

        Assert.False((await svc.AssignTaskAsync(owner, taskInP1, sprintInP2.Id)).Succeeded);
    }

    [Fact]
    public async Task AssignTask_NullSprint_ReturnsToBacklog()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var project = await TestDb.AddProjectAsync(ctx, owner, "P");
        var svc = Make(ctx);
        var sprint = (await svc.CreateSprintAsync(owner, project, new SaveSprintDto { Name = "S" })).Value!;
        var task = await SeedTaskAsync(ctx, owner, project);
        await svc.AssignTaskAsync(owner, task, sprint.Id);

        Assert.True((await svc.AssignTaskAsync(owner, task, null)).Succeeded);
        Assert.Null((await ctx.ProjectTasks.FindAsync(task))!.SprintId);
    }

    [Fact]
    public async Task Update_ChangesStatusAndName()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var project = await TestDb.AddProjectAsync(ctx, owner, "P");
        var svc = Make(ctx);
        var sprint = (await svc.CreateSprintAsync(owner, project, new SaveSprintDto { Name = "S1" })).Value!;

        var updated = await svc.UpdateSprintAsync(owner, sprint.Id, new SaveSprintDto { Name = "Sprint One", Status = "Active" });

        Assert.True(updated.Succeeded);
        Assert.Equal("Sprint One", updated.Value!.Name);
        Assert.Equal("Active", updated.Value.Status);
    }

    [Fact]
    public async Task Delete_RemovesTheSprint()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var project = await TestDb.AddProjectAsync(ctx, owner, "P");
        var svc = Make(ctx);
        var sprint = (await svc.CreateSprintAsync(owner, project, new SaveSprintDto { Name = "S" })).Value!;

        Assert.True((await svc.DeleteSprintAsync(owner, sprint.Id)).Succeeded);
        Assert.Empty((await svc.GetSprintsAsync(owner, project)).Value!);
    }

    [Fact]
    public async Task GetSprints_SumsPlannedAndCompletedStoryPoints()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var project = await TestDb.AddProjectAsync(ctx, owner, "P");
        var svc = Make(ctx);
        var sprint = (await svc.CreateSprintAsync(owner, project, new SaveSprintDto { Name = "S" })).Value!;

        // 5 pts done, 3 pts open, and a done task with no estimate (counts 0 points).
        void Add(ProjectTaskStatus status, int? estimate) => ctx.ProjectTasks.Add(new ProjectTask
        {
            Id = Guid.NewGuid(), ProjectId = project, CreatorId = owner, Title = "T",
            Status = status, SprintId = sprint.Id, Estimate = estimate,
        });
        Add(ProjectTaskStatus.Done, 5);
        Add(ProjectTaskStatus.Backlog, 3);
        Add(ProjectTaskStatus.Done, null);
        await ctx.SaveChangesAsync();

        var s = (await svc.GetSprintsAsync(owner, project)).Value!.Single();
        Assert.Equal(3, s.TaskCount);
        Assert.Equal(2, s.DoneCount);
        Assert.Equal(8, s.PlannedPoints);   // 5 + 3 + 0
        Assert.Equal(5, s.CompletedPoints); // only the done+estimated task
    }
}
