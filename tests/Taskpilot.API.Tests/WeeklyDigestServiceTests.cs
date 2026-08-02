using Microsoft.Extensions.Logging.Abstractions;
using Taskpilot.API.Common;
using Taskpilot.API.Data;
using Taskpilot.API.Models;
using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>Tests for the weekly digest: deterministic counts + the LLM narrative (faked).</summary>
public class WeeklyDigestServiceTests
{
    private sealed class FakeLlm : IChatBotClient
    {
        public bool IsEnabled { get; set; } = true;
        public Result<string> Reply { get; set; } = Result<string>.Ok("You had a productive week!");
        public Task<Result<string>> CompleteAsync(IReadOnlyList<ChatBotMessage> messages) => Task.FromResult(Reply);
    }

    private static WeeklyDigestService Make(TaskpilotDbContext ctx, IChatBotClient llm) =>
        new(ctx, llm, NullLogger<WeeklyDigestService>.Instance);

    private static void AddTask(TaskpilotDbContext ctx, Guid projectId, Guid owner, string title,
        ProjectTaskStatus status, DateTime createdAt, DateTime? completedAt = null, DateTime? deadline = null) =>
        ctx.ProjectTasks.Add(new ProjectTask
        {
            Id = Guid.NewGuid(), ProjectId = projectId, CreatorId = owner, Title = title,
            Status = status, CreatedAt = createdAt, CompletedAt = completedAt, Deadline = deadline,
        });

    [Fact]
    public async Task GetWeekly_CountsWithinTheRightWindows()
    {
        await using var ctx = TestDb.CreateContext();
        var user = await TestDb.AddUserAsync(ctx, "U");
        var project = await TestDb.AddProjectAsync(ctx, user, "P");
        var now = DateTime.UtcNow;

        AddTask(ctx, project, user, "Shipped", ProjectTaskStatus.Done, now.AddDays(-2), completedAt: now.AddDays(-2));
        AddTask(ctx, project, user, "Soon", ProjectTaskStatus.Backlog, now.AddDays(-3), deadline: now.AddDays(2));
        AddTask(ctx, project, user, "Late", ProjectTaskStatus.InProgress, now.AddDays(-30), deadline: now.AddDays(-1));
        AddTask(ctx, project, user, "Old idle", ProjectTaskStatus.Backlog, now.AddDays(-30));
        await ctx.SaveChangesAsync();

        var digest = await Make(ctx, new FakeLlm()).GetWeeklyAsync(user);

        Assert.Equal(1, digest.Completed);              // "Shipped"
        Assert.Equal(2, digest.Created);                // "Shipped" + "Soon" (within 7 days)
        Assert.Equal(1, digest.Overdue);                // "Late"
        Assert.Equal(1, digest.DueSoon);                // "Soon"
        Assert.Contains("Shipped", digest.TopCompleted);
    }

    [Fact]
    public async Task GetSummary_UsesTheLlmText_WhenEnabled()
    {
        await using var ctx = TestDb.CreateContext();
        var user = await TestDb.AddUserAsync(ctx, "U");
        var svc = Make(ctx, new FakeLlm { Reply = Result<string>.Ok("Great progress this week.") });

        var result = await svc.GetSummaryAsync(user);

        Assert.True(result.Value!.Enabled);
        Assert.Equal("Great progress this week.", result.Value.Summary);
    }

    [Fact]
    public async Task GetSummary_IsDisabled_WithoutLlm()
    {
        await using var ctx = TestDb.CreateContext();
        var user = await TestDb.AddUserAsync(ctx, "U");
        var svc = Make(ctx, new FakeLlm { IsEnabled = false });

        Assert.False(svc.IsEnabled);
        var result = await svc.GetSummaryAsync(user);
        Assert.True(result.Succeeded);
        Assert.False(result.Value!.Enabled);
        Assert.Equal(string.Empty, result.Value.Summary);
    }

    [Fact]
    public async Task GetSummary_Fails_WhenLlmErrors()
    {
        await using var ctx = TestDb.CreateContext();
        var user = await TestDb.AddUserAsync(ctx, "U");
        var svc = Make(ctx, new FakeLlm { Reply = Result<string>.Fail("rate limited") });

        Assert.False((await svc.GetSummaryAsync(user)).Succeeded);
    }
}
