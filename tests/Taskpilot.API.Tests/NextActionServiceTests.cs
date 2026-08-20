using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Taskpilot.API.Common;
using Taskpilot.API.Data;
using Taskpilot.API.Models;
using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>
/// Tests for the "what to do next" planner: the deterministic urgency order and blocked/scope
/// rules (no LLM), plus the AI re-ranking path with a stubbed chat client.
/// </summary>
public class NextActionServiceTests
{
    private static NextActionService Make(TaskpilotDbContext ctx, Mock<IChatBotClient> llm) =>
        new(ctx, llm.Object, NullLogger<NextActionService>.Instance);

    private static Mock<IChatBotClient> Llm(bool enabled, string? reply = null)
    {
        var mock = new Mock<IChatBotClient>();
        mock.SetupGet(c => c.IsEnabled).Returns(enabled);
        mock.Setup(c => c.CompleteAsync(It.IsAny<IReadOnlyList<ChatBotMessage>>()))
            .ReturnsAsync(reply is null ? Result<string>.Fail("no key") : Result<string>.Ok(reply));
        return mock;
    }

    private static async Task<Guid> AddTaskAsync(
        TaskpilotDbContext ctx, Guid projectId, Guid? assignee, string title,
        TaskPriority priority = TaskPriority.Medium, DateTime? deadline = null,
        ProjectTaskStatus status = ProjectTaskStatus.InProgress)
    {
        var id = Guid.NewGuid();
        ctx.ProjectTasks.Add(new ProjectTask
        {
            Id = id, ProjectId = projectId, Title = title, CreatorId = assignee ?? Guid.NewGuid(),
            AssigneeId = assignee, Priority = priority, Deadline = deadline, Status = status,
        });
        await ctx.SaveChangesAsync();
        return id;
    }

    [Fact]
    public async Task Deterministic_OrdersOverdueThenDueSoon_AndFlagsDisabled()
    {
        await using var ctx = TestDb.CreateContext();
        var user = await TestDb.AddUserAsync(ctx, "U");
        var project = await TestDb.AddProjectAsync(ctx, user);
        var soon = await AddTaskAsync(ctx, project, user, "Due soon", TaskPriority.Low, DateTime.UtcNow.AddDays(1));
        var overdue = await AddTaskAsync(ctx, project, user, "Overdue", TaskPriority.Low, DateTime.UtcNow.AddDays(-1));
        var noDate = await AddTaskAsync(ctx, project, user, "No deadline", TaskPriority.High, null);

        var plan = await Make(ctx, Llm(enabled: false)).GetPlanAsync(user);

        Assert.False(plan.Enabled);
        Assert.False(plan.RankedByAi);
        Assert.Equal(new[] { overdue, soon, noDate }, plan.Items.Select(i => i.TaskId).ToArray());
        Assert.All(plan.Items, i => Assert.Null(i.Reason));
        Assert.True(plan.Items[0].IsOverdue);
    }

    [Fact]
    public async Task Excludes_Done_Unassigned_AndArchivedProjects()
    {
        await using var ctx = TestDb.CreateContext();
        var user = await TestDb.AddUserAsync(ctx, "U");
        var project = await TestDb.AddProjectAsync(ctx, user);
        var mine = await AddTaskAsync(ctx, project, user, "Mine");
        await AddTaskAsync(ctx, project, user, "Done", status: ProjectTaskStatus.Done);
        await AddTaskAsync(ctx, project, assignee: null, "Unassigned");

        var archived = await TestDb.AddProjectAsync(ctx, user, "Archived");
        (await ctx.Projects.FindAsync(archived))!.ArchivedAt = DateTime.UtcNow;
        await ctx.SaveChangesAsync();
        await AddTaskAsync(ctx, archived, user, "In archived project");

        var plan = await Make(ctx, Llm(enabled: false)).GetPlanAsync(user);

        Assert.Equal(new[] { mine }, plan.Items.Select(i => i.TaskId).ToArray());
    }

    [Fact]
    public async Task BlockedTask_IsFlagged_AndSortedLast()
    {
        await using var ctx = TestDb.CreateContext();
        var user = await TestDb.AddUserAsync(ctx, "U");
        var project = await TestDb.AddProjectAsync(ctx, user);
        // A high-priority task, but blocked by an unfinished dependency.
        var blocked = await AddTaskAsync(ctx, project, user, "Blocked", TaskPriority.High);
        var blocker = await AddTaskAsync(ctx, project, user, "Blocker", TaskPriority.Low);
        var free = await AddTaskAsync(ctx, project, user, "Free", TaskPriority.Low);
        ctx.TaskDependencies.Add(new TaskDependency { Id = Guid.NewGuid(), TaskId = blocked, DependsOnTaskId = blocker });
        await ctx.SaveChangesAsync();

        var plan = await Make(ctx, Llm(enabled: false)).GetPlanAsync(user);

        var blockedItem = plan.Items.Single(i => i.TaskId == blocked);
        Assert.True(blockedItem.IsBlocked);
        // Startable tasks come first; the blocked one lands last.
        Assert.Equal(blocked, plan.Items.Last().TaskId);
    }

    [Fact]
    public async Task AiRanking_ReordersPoolAndAttachesReasons()
    {
        await using var ctx = TestDb.CreateContext();
        var user = await TestDb.AddUserAsync(ctx, "U");
        var project = await TestDb.AddProjectAsync(ctx, user);
        var overdue = await AddTaskAsync(ctx, project, user, "Overdue", TaskPriority.Low, DateTime.UtcNow.AddDays(-1)); // pool #1
        var noDate = await AddTaskAsync(ctx, project, user, "No deadline", TaskPriority.High, null);                   // pool #2

        // The model prefers #2 over #1 and explains each.
        var plan = await Make(ctx, Llm(enabled: true, reply: "2: high priority quick win\n1: it is overdue")).GetPlanAsync(user);

        Assert.True(plan.Enabled);
        Assert.True(plan.RankedByAi);
        Assert.Equal(new[] { noDate, overdue }, plan.Items.Select(i => i.TaskId).ToArray());
        Assert.Equal("high priority quick win", plan.Items[0].Reason);
        Assert.Equal("it is overdue", plan.Items[1].Reason);
    }

    [Fact]
    public async Task AiRanking_SurvivesAnOutOfRangeNumber_FromTheModel()
    {
        await using var ctx = TestDb.CreateContext();
        var user = await TestDb.AddUserAsync(ctx, "U");
        var project = await TestDb.AddProjectAsync(ctx, user);
        var overdue = await AddTaskAsync(ctx, project, user, "Overdue", TaskPriority.Low, DateTime.UtcNow.AddDays(-1)); // pool #1
        var noDate = await AddTaskAsync(ctx, project, user, "No deadline", TaskPriority.High, null);                   // pool #2

        // A hallucinated line with a number too large for int must be skipped, not crash the parse.
        var plan = await Make(ctx, Llm(enabled: true, reply: "99999999999: bogus\n2: quick win\n1: overdue")).GetPlanAsync(user);

        Assert.True(plan.RankedByAi);
        Assert.Equal(new[] { noDate, overdue }, plan.Items.Select(i => i.TaskId).ToArray());
    }

    [Fact]
    public async Task AiFailure_FallsBackToDeterministic_ButStaysEnabled()
    {
        await using var ctx = TestDb.CreateContext();
        var user = await TestDb.AddUserAsync(ctx, "U");
        var project = await TestDb.AddProjectAsync(ctx, user);
        var overdue = await AddTaskAsync(ctx, project, user, "Overdue", TaskPriority.Low, DateTime.UtcNow.AddDays(-1));
        var soon = await AddTaskAsync(ctx, project, user, "Soon", TaskPriority.Low, DateTime.UtcNow.AddDays(2));

        // Enabled, but the model errors → deterministic order, no reasons, RankedByAi false.
        var plan = await Make(ctx, Llm(enabled: true, reply: null)).GetPlanAsync(user);

        Assert.True(plan.Enabled);
        Assert.False(plan.RankedByAi);
        Assert.Equal(new[] { overdue, soon }, plan.Items.Select(i => i.TaskId).ToArray());
    }
}
