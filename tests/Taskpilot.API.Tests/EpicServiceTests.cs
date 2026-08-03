using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Projects;
using Taskpilot.API.Models;
using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>Tests for epics: creation, task tallies, assign/ungroup, cross-project guard and access.</summary>
public class EpicServiceTests
{
    private static EpicService Make(TaskpilotDbContext ctx) => new(ctx, NullLogger<EpicService>.Instance);

    private static async Task<Guid> SeedTaskAsync(TaskpilotDbContext ctx, Guid owner, Guid projectId,
        Guid? epicId = null, ProjectTaskStatus status = ProjectTaskStatus.Backlog)
    {
        var id = Guid.NewGuid();
        ctx.ProjectTasks.Add(new ProjectTask { Id = id, ProjectId = projectId, CreatorId = owner, Title = "T", EpicId = epicId, Status = status });
        await ctx.SaveChangesAsync();
        return id;
    }

    [Fact]
    public async Task Create_ThenGet_WithTaskTallies()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var project = await TestDb.AddProjectAsync(ctx, owner, "P");
        var svc = Make(ctx);

        var epic = (await svc.CreateEpicAsync(owner, project, new SaveEpicDto { Title = "Checkout revamp", Color = "#ff0000" })).Value!;
        await SeedTaskAsync(ctx, owner, project, epic.Id, ProjectTaskStatus.Done);
        await SeedTaskAsync(ctx, owner, project, epic.Id);

        var epics = (await svc.GetEpicsAsync(owner, project)).Value!;
        var got = Assert.Single(epics);
        Assert.Equal("Checkout revamp", got.Title);
        Assert.Equal("#ff0000", got.Color);
        Assert.Equal(2, got.TaskCount);
        Assert.Equal(1, got.DoneCount);
    }

    [Fact]
    public async Task AssignTask_MovesIntoEpic_AndUngroups()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var project = await TestDb.AddProjectAsync(ctx, owner, "P");
        var svc = Make(ctx);
        var epic = (await svc.CreateEpicAsync(owner, project, new SaveEpicDto { Title = "E" })).Value!;
        var task = await SeedTaskAsync(ctx, owner, project);

        Assert.True((await svc.AssignTaskAsync(owner, task, epic.Id)).Succeeded);
        Assert.Equal(epic.Id, (await ctx.ProjectTasks.FindAsync(task))!.EpicId);

        Assert.True((await svc.AssignTaskAsync(owner, task, null)).Succeeded);
        Assert.Null((await ctx.ProjectTasks.FindAsync(task))!.EpicId);
    }

    [Fact]
    public async Task AssignTask_FromAnotherProject_Fails()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var a = await TestDb.AddProjectAsync(ctx, owner, "A");
        var b = await TestDb.AddProjectAsync(ctx, owner, "B");
        var svc = Make(ctx);
        var epicB = (await svc.CreateEpicAsync(owner, b, new SaveEpicDto { Title = "E" })).Value!;
        var taskA = await SeedTaskAsync(ctx, owner, a);

        Assert.False((await svc.AssignTaskAsync(owner, taskA, epicB.Id)).Succeeded);
    }

    [Fact]
    public async Task Delete_RemovesTheEpic()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var project = await TestDb.AddProjectAsync(ctx, owner, "P");
        var svc = Make(ctx);
        var epic = (await svc.CreateEpicAsync(owner, project, new SaveEpicDto { Title = "E" })).Value!;

        Assert.True((await svc.DeleteEpicAsync(owner, epic.Id)).Succeeded);
        Assert.Empty((await svc.GetEpicsAsync(owner, project)).Value!);
    }

    [Fact]
    public async Task Create_WithoutWriteAccess_Fails()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var stranger = await TestDb.AddUserAsync(ctx, "Stranger");
        var project = await TestDb.AddProjectAsync(ctx, owner, "P");

        Assert.False((await Make(ctx).CreateEpicAsync(stranger, project, new SaveEpicDto { Title = "E" })).Succeeded);
    }
}
