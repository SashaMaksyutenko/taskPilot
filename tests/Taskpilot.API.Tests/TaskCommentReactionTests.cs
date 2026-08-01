using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Taskpilot.API.Data;
using Taskpilot.API.Models;
using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>Tests for emoji reactions on task comments: toggle, aggregation, access guard and listing.</summary>
public class TaskCommentReactionTests
{
    private static TaskCommentService Make(TaskpilotDbContext ctx) =>
        new(ctx, new Mock<IWebhookService>().Object, new Mock<INotificationService>().Object,
            NullLogger<TaskCommentService>.Instance);

    private static async Task<(Guid taskId, Guid commentId)> SeedCommentAsync(
        TaskpilotDbContext ctx, Guid owner, Guid projectId, Guid authorId)
    {
        var taskId = Guid.NewGuid();
        ctx.ProjectTasks.Add(new ProjectTask { Id = taskId, ProjectId = projectId, CreatorId = owner, Title = "T" });
        var commentId = Guid.NewGuid();
        ctx.TaskComments.Add(new TaskComment { Id = commentId, TaskId = taskId, AuthorId = authorId, Body = "hi" });
        await ctx.SaveChangesAsync();
        return (taskId, commentId);
    }

    [Fact]
    public async Task Toggle_AddsThenRemoves()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var project = await TestDb.AddProjectAsync(ctx, owner, "P");
        var (_, commentId) = await SeedCommentAsync(ctx, owner, project, owner);
        var svc = Make(ctx);

        var added = await svc.ToggleReactionAsync(owner, commentId, "👍");
        Assert.True(added.Succeeded);
        var group = Assert.Single(added.Value!.Reactions);
        Assert.Equal("👍", group.Emoji);
        Assert.Equal(1, group.Count);
        Assert.True(group.Mine);

        // Toggling the same emoji again removes it.
        var removed = await svc.ToggleReactionAsync(owner, commentId, "👍");
        Assert.Empty(removed.Value!.Reactions);
    }

    [Fact]
    public async Task Toggle_AggregatesAcrossUsers()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var member = await TestDb.AddUserAsync(ctx, "Member");
        var project = await TestDb.AddProjectAsync(ctx, owner, "P");
        ctx.ProjectMembers.Add(new ProjectMember { Id = Guid.NewGuid(), ProjectId = project, UserId = member });
        await ctx.SaveChangesAsync();
        var (_, commentId) = await SeedCommentAsync(ctx, owner, project, owner);
        var svc = Make(ctx);

        await svc.ToggleReactionAsync(owner, commentId, "👍");
        var memberView = await svc.ToggleReactionAsync(member, commentId, "👍");

        var group = Assert.Single(memberView.Value!.Reactions);
        Assert.Equal(2, group.Count);
        Assert.True(group.Mine); // the member is one of the two reactors
    }

    [Fact]
    public async Task Get_IncludesDistinctReactionGroups()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var project = await TestDb.AddProjectAsync(ctx, owner, "P");
        var (taskId, commentId) = await SeedCommentAsync(ctx, owner, project, owner);
        var svc = Make(ctx);

        await svc.ToggleReactionAsync(owner, commentId, "👍");
        await svc.ToggleReactionAsync(owner, commentId, "🎉");

        var comments = (await svc.GetForTaskAsync(owner, taskId)).Value!;
        var comment = Assert.Single(comments);
        Assert.Equal(2, comment.Reactions.Count);
        Assert.All(comment.Reactions, r => Assert.Equal(1, r.Count));
    }

    [Fact]
    public async Task Toggle_WithoutProjectAccess_Fails()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var stranger = await TestDb.AddUserAsync(ctx, "Stranger");
        var project = await TestDb.AddProjectAsync(ctx, owner, "P");
        var (_, commentId) = await SeedCommentAsync(ctx, owner, project, owner);

        Assert.False((await Make(ctx).ToggleReactionAsync(stranger, commentId, "👍")).Succeeded);
    }

    [Fact]
    public async Task Toggle_InvalidEmoji_Fails()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var project = await TestDb.AddProjectAsync(ctx, owner, "P");
        var (_, commentId) = await SeedCommentAsync(ctx, owner, project, owner);

        Assert.False((await Make(ctx).ToggleReactionAsync(owner, commentId, "  ")).Succeeded);
    }
}
