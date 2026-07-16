using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Taskpilot.API.Common;
using Taskpilot.API.Data;
using Taskpilot.API.Models;
using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>
/// Unit tests for <see cref="TaskAiService"/> with a mocked <see cref="IChatBotClient"/> —
/// no OpenAI calls. Covers config-gating, access, and parsing the model's reply.
/// </summary>
public class TaskAiServiceTests
{
    private static (TaskAiService svc, Mock<IChatBotClient> client) Create(TaskpilotDbContext ctx, bool enabled = true)
    {
        var client = new Mock<IChatBotClient>();
        client.SetupGet(c => c.IsEnabled).Returns(enabled);
        return (new TaskAiService(ctx, client.Object, NullLogger<TaskAiService>.Instance), client);
    }

    private static async Task<Guid> SeedTaskAsync(TaskpilotDbContext ctx, Guid ownerId, string title = "Build login")
    {
        var projectId = await TestDb.AddProjectAsync(ctx, ownerId);
        var taskId = Guid.NewGuid();
        ctx.ProjectTasks.Add(new ProjectTask
        {
            Id = taskId, ProjectId = projectId, Title = title,
            Description = "Email + password auth", CreatorId = ownerId,
        });
        await ctx.SaveChangesAsync();
        return taskId;
    }

    [Fact]
    public async Task NotConfigured_Fails()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var taskId = await SeedTaskAsync(ctx, owner);
        var (svc, _) = Create(ctx, enabled: false);

        Assert.False(svc.IsEnabled);
        Assert.False((await svc.SuggestSubtasksAsync(owner, taskId)).Succeeded);
    }

    [Fact]
    public async Task NonMember_Fails()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var outsider = await TestDb.AddUserAsync(ctx, "Outsider");
        var taskId = await SeedTaskAsync(ctx, owner);
        var (svc, client) = Create(ctx);

        var result = await svc.SuggestSubtasksAsync(outsider, taskId);

        Assert.False(result.Succeeded);
        // The model is never called when access is denied.
        client.Verify(c => c.CompleteAsync(It.IsAny<IReadOnlyList<ChatBotMessage>>()), Times.Never);
    }

    [Fact]
    public async Task ParsesBulletsAndNumbering_IntoCleanTitles()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var taskId = await SeedTaskAsync(ctx, owner);
        var (svc, client) = Create(ctx);
        // A messy reply: bullets, numbering, blank lines, a duplicate.
        client.Setup(c => c.CompleteAsync(It.IsAny<IReadOnlyList<ChatBotMessage>>()))
            .ReturnsAsync(Result<string>.Ok(
                "- Design the schema\n" +
                "1. Build the API\n" +
                "* Build the API\n" +   // duplicate → dropped
                "\n" +
                "• Write tests"));

        var result = await svc.SuggestSubtasksAsync(owner, taskId);

        Assert.True(result.Succeeded);
        Assert.Equal(new[] { "Design the schema", "Build the API", "Write tests" }, result.Value);
    }

    [Fact]
    public async Task CapsAtEightSuggestions()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var taskId = await SeedTaskAsync(ctx, owner);
        var (svc, client) = Create(ctx);
        var many = string.Join('\n', Enumerable.Range(1, 20).Select(i => $"Step {i}"));
        client.Setup(c => c.CompleteAsync(It.IsAny<IReadOnlyList<ChatBotMessage>>()))
            .ReturnsAsync(Result<string>.Ok(many));

        var result = await svc.SuggestSubtasksAsync(owner, taskId);

        Assert.True(result.Succeeded);
        Assert.Equal(8, result.Value!.Count);
    }

    [Fact]
    public async Task EmptyReply_Fails()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var taskId = await SeedTaskAsync(ctx, owner);
        var (svc, client) = Create(ctx);
        client.Setup(c => c.CompleteAsync(It.IsAny<IReadOnlyList<ChatBotMessage>>()))
            .ReturnsAsync(Result<string>.Ok("\n\n  \n"));

        Assert.False((await svc.SuggestSubtasksAsync(owner, taskId)).Succeeded);
    }

    [Fact]
    public async Task SendsTaskTitleAndDescriptionToTheModel()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var taskId = await SeedTaskAsync(ctx, owner, "Ship beta");
        var (svc, client) = Create(ctx);
        IReadOnlyList<ChatBotMessage>? captured = null;
        client.Setup(c => c.CompleteAsync(It.IsAny<IReadOnlyList<ChatBotMessage>>()))
            .Callback<IReadOnlyList<ChatBotMessage>>(m => captured = m)
            .ReturnsAsync(Result<string>.Ok("Task A"));

        await svc.SuggestSubtasksAsync(owner, taskId);

        Assert.NotNull(captured);
        Assert.Equal("system", captured![0].Role);
        Assert.Contains("Ship beta", captured[^1].Content);
    }
}
