using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Projects;
using Taskpilot.API.Models;
using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>Tests that commenting (or being @mentioned) auto-subscribes a user to the task.</summary>
public class TaskCommentWatchTests
{
    private static TaskCommentService Make(TaskpilotDbContext ctx) =>
        new(ctx, new Mock<IWebhookService>().Object, new Mock<INotificationService>().Object,
            NullLogger<TaskCommentService>.Instance);

    private static async Task<Guid> SeedTaskAsync(TaskpilotDbContext ctx, Guid owner, Guid projectId)
    {
        var id = Guid.NewGuid();
        ctx.ProjectTasks.Add(new ProjectTask { Id = id, ProjectId = projectId, CreatorId = owner, Title = "T" });
        await ctx.SaveChangesAsync();
        return id;
    }

    private static Task<bool> Watching(TaskpilotDbContext ctx, Guid taskId, Guid userId) =>
        ctx.TaskWatchers.AnyAsync(w => w.TaskId == taskId && w.UserId == userId);

    [Fact]
    public async Task Commenting_SubscribesTheAuthor()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var project = await TestDb.AddProjectAsync(ctx, owner, "P");
        var task = await SeedTaskAsync(ctx, owner, project);

        var result = await Make(ctx).AddAsync(owner, task, new CreateCommentDto { Body = "just a note" });

        Assert.True(result.Succeeded);
        Assert.True(await Watching(ctx, task, owner));
    }

    [Fact]
    public async Task Mentioning_SubscribesTheMentionedUser()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var bob = await TestDb.AddUserAsync(ctx, "Bob");
        var project = await TestDb.AddProjectAsync(ctx, owner, "P");
        ctx.ProjectMembers.Add(new ProjectMember { Id = Guid.NewGuid(), ProjectId = project, UserId = bob });
        await ctx.SaveChangesAsync();
        var task = await SeedTaskAsync(ctx, owner, project);

        await Make(ctx).AddAsync(owner, task, new CreateCommentDto { Body = "@Bob please review" });

        Assert.True(await Watching(ctx, task, bob));   // mentioned → subscribed
        Assert.True(await Watching(ctx, task, owner));  // author → subscribed
    }

    [Fact]
    public async Task Commenting_Twice_KeepsASingleWatchRow()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var project = await TestDb.AddProjectAsync(ctx, owner, "P");
        var task = await SeedTaskAsync(ctx, owner, project);
        var svc = Make(ctx);

        await svc.AddAsync(owner, task, new CreateCommentDto { Body = "first" });
        await svc.AddAsync(owner, task, new CreateCommentDto { Body = "second" });

        Assert.Equal(1, await ctx.TaskWatchers.CountAsync(w => w.TaskId == task && w.UserId == owner));
    }
}
