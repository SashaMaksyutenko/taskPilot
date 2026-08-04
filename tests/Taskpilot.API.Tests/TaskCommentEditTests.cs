using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Taskpilot.API.Data;
using Taskpilot.API.Models;
using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>Tests for editing task comments: author-only, validation and reaction preservation.</summary>
public class TaskCommentEditTests
{
    private static TaskCommentService Make(TaskpilotDbContext ctx) =>
        new(ctx, new Mock<IWebhookService>().Object, new Mock<INotificationService>().Object,
            NullLogger<TaskCommentService>.Instance);

    private static async Task<Guid> SeedCommentAsync(TaskpilotDbContext ctx, Guid owner, Guid projectId, Guid authorId)
    {
        var taskId = Guid.NewGuid();
        ctx.ProjectTasks.Add(new ProjectTask { Id = taskId, ProjectId = projectId, CreatorId = owner, Title = "T" });
        var commentId = Guid.NewGuid();
        ctx.TaskComments.Add(new TaskComment { Id = commentId, TaskId = taskId, AuthorId = authorId, Body = "original" });
        await ctx.SaveChangesAsync();
        return commentId;
    }

    [Fact]
    public async Task Edit_UpdatesBody_AndSetsUpdatedAt()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var project = await TestDb.AddProjectAsync(ctx, owner, "P");
        var commentId = await SeedCommentAsync(ctx, owner, project, owner);

        var result = await Make(ctx).EditAsync(owner, commentId, "  edited text  ");

        Assert.True(result.Succeeded);
        Assert.Equal("edited text", result.Value!.Body); // trimmed
        Assert.NotNull(result.Value.UpdatedAt);
        Assert.Equal("edited text", (await ctx.TaskComments.FindAsync(commentId))!.Body);
    }

    [Fact]
    public async Task Edit_ByNonAuthor_Fails()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var other = await TestDb.AddUserAsync(ctx, "Other");
        var project = await TestDb.AddProjectAsync(ctx, owner, "P");
        var commentId = await SeedCommentAsync(ctx, owner, project, owner);

        Assert.False((await Make(ctx).EditAsync(other, commentId, "hijack")).Succeeded);
    }

    [Fact]
    public async Task Edit_EmptyBody_Fails()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var project = await TestDb.AddProjectAsync(ctx, owner, "P");
        var commentId = await SeedCommentAsync(ctx, owner, project, owner);

        Assert.False((await Make(ctx).EditAsync(owner, commentId, "   ")).Succeeded);
    }

    [Fact]
    public async Task Edit_PreservesReactions()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var project = await TestDb.AddProjectAsync(ctx, owner, "P");
        var commentId = await SeedCommentAsync(ctx, owner, project, owner);
        ctx.TaskCommentReactions.Add(new TaskCommentReaction { Id = Guid.NewGuid(), CommentId = commentId, UserId = owner, Emoji = "👍" });
        await ctx.SaveChangesAsync();

        var result = await Make(ctx).EditAsync(owner, commentId, "updated");

        var reaction = Assert.Single(result.Value!.Reactions);
        Assert.Equal("👍", reaction.Emoji);
        Assert.True(reaction.Mine);
    }
}
