using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Projects;
using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>Unit tests for <see cref="TaskService"/> over an in-memory database.</summary>
public class TaskServiceTests
{
    private static TaskService Create(TaskpilotDbContext ctx) =>
        new(ctx, new Mock<IWebhookService>().Object, new Mock<INotificationService>().Object, NullLogger<TaskService>.Instance);

    [Fact]
    public async Task CreateTask_DefaultsToBacklogAndMedium()
    {
        using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx);
        var projectId = await TestDb.AddProjectAsync(ctx, owner);
        var svc = Create(ctx);

        var result = await svc.CreateTaskAsync(owner, projectId, new CreateTaskDto { Title = "Task 1" });

        Assert.True(result.Succeeded);
        Assert.Equal("Backlog", result.Value!.Status);
        Assert.Equal("Medium", result.Value.Priority);
    }

    [Fact]
    public async Task CreateTask_InvalidPriority_Fails()
    {
        using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx);
        var projectId = await TestDb.AddProjectAsync(ctx, owner);
        var svc = Create(ctx);

        var result = await svc.CreateTaskAsync(owner, projectId, new CreateTaskDto { Title = "X", Priority = "Urgent" });

        Assert.False(result.Succeeded);
        Assert.Equal("Invalid priority.", result.Error);
    }

    [Fact]
    public async Task CreateTask_NotOwnedProject_Fails()
    {
        using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var other = await TestDb.AddUserAsync(ctx, "Other");
        var projectId = await TestDb.AddProjectAsync(ctx, owner);
        var svc = Create(ctx);

        var result = await svc.CreateTaskAsync(other, projectId, new CreateTaskDto { Title = "Sneaky" });

        Assert.False(result.Succeeded);
        Assert.Equal("Project not found.", result.Error);
    }

    [Fact]
    public async Task CreateTask_AssignedToNonMember_GrantsThemProjectAccess()
    {
        using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var assignee = await TestDb.AddUserAsync(ctx, "Assignee");
        var projectId = await TestDb.AddProjectAsync(ctx, owner);
        var svc = Create(ctx);

        // Before: the assignee cannot access the project.
        Assert.False(await ProjectAccess.CanAccessAsync(ctx, projectId, assignee));

        var result = await svc.CreateTaskAsync(owner, projectId,
            new CreateTaskDto { Title = "Do the thing", AssigneeId = assignee });

        Assert.True(result.Succeeded);
        // After: assigning the task made them an Editor member with access.
        Assert.True(await ProjectAccess.CanAccessAsync(ctx, projectId, assignee));
        Assert.True(await ProjectAccess.CanWriteAsync(ctx, projectId, assignee));
        Assert.Equal(1, await ctx.ProjectMembers.CountAsync(m => m.ProjectId == projectId && m.UserId == assignee));
    }

    [Fact]
    public async Task CreateTask_AssignedToOwner_AddsNoMemberRow()
    {
        using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var projectId = await TestDb.AddProjectAsync(ctx, owner);
        var svc = Create(ctx);

        await svc.CreateTaskAsync(owner, projectId,
            new CreateTaskDto { Title = "Mine", AssigneeId = owner });

        // The owner already has access, so no redundant membership is created.
        Assert.Equal(0, await ctx.ProjectMembers.CountAsync(m => m.ProjectId == projectId));
    }

    [Fact]
    public async Task GetTasks_FiltersByStatus()
    {
        using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx);
        var projectId = await TestDb.AddProjectAsync(ctx, owner);
        var svc = Create(ctx);

        var t1 = await svc.CreateTaskAsync(owner, projectId, new CreateTaskDto { Title = "A" });
        await svc.CreateTaskAsync(owner, projectId, new CreateTaskDto { Title = "B" });
        await svc.ChangeStatusAsync(owner, t1.Value!.Id, "InProgress");

        var backlog = await svc.GetTasksAsync(owner, projectId, "Backlog");
        var inProgress = await svc.GetTasksAsync(owner, projectId, "InProgress");

        Assert.Single(backlog.Value!);
        Assert.Single(inProgress.Value!);
    }

    [Fact]
    public async Task ChangeStatus_ToDone_SetsCompletedAt_AndClearsWhenLeaving()
    {
        using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx);
        var projectId = await TestDb.AddProjectAsync(ctx, owner);
        var svc = Create(ctx);
        var task = await svc.CreateTaskAsync(owner, projectId, new CreateTaskDto { Title = "T" });

        var done = await svc.ChangeStatusAsync(owner, task.Value!.Id, "Done");
        Assert.NotNull(done.Value!.CompletedAt);

        var reopened = await svc.ChangeStatusAsync(owner, task.Value.Id, "InProgress");
        Assert.Null(reopened.Value!.CompletedAt);
    }

    [Fact]
    public async Task ChangeStatus_NonOwner_Fails()
    {
        using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var other = await TestDb.AddUserAsync(ctx, "Other");
        var projectId = await TestDb.AddProjectAsync(ctx, owner);
        var svc = Create(ctx);
        var task = await svc.CreateTaskAsync(owner, projectId, new CreateTaskDto { Title = "T" });

        var result = await svc.ChangeStatusAsync(other, task.Value!.Id, "Done");

        Assert.False(result.Succeeded);
        Assert.Equal("Task not found.", result.Error);
    }

    [Fact]
    public async Task DeleteTask_RemovesIt()
    {
        using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx);
        var projectId = await TestDb.AddProjectAsync(ctx, owner);
        var svc = Create(ctx);
        var task = await svc.CreateTaskAsync(owner, projectId, new CreateTaskDto { Title = "T" });

        var result = await svc.DeleteTaskAsync(owner, task.Value!.Id);

        Assert.True(result.Succeeded);
        Assert.Equal(0, await ctx.ProjectTasks.CountAsync());
    }
}
