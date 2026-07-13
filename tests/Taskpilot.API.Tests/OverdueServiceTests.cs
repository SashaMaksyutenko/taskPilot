using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Taskpilot.API.Common;
using Taskpilot.API.Data;
using Taskpilot.API.Models;
using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>
/// Unit tests for <see cref="OverdueService"/>, focused on the escalation pass
/// (tasks overdue past the 3-day threshold notify the whole team + fire a webhook).
/// </summary>
public class OverdueServiceTests
{
    private static (OverdueService svc, Mock<INotificationService> notifications, Mock<IWebhookService> webhooks) Create(TaskpilotDbContext ctx)
    {
        var notifications = new Mock<INotificationService>();
        notifications
            .Setup(n => n.CreateAsync(It.IsAny<Guid>(), It.IsAny<NotificationType>(), It.IsAny<string>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);
        var webhooks = new Mock<IWebhookService>();
        webhooks
            .Setup(w => w.DispatchAsync(It.IsAny<string>(), It.IsAny<object>()))
            .Returns(Task.CompletedTask);
        return (new OverdueService(ctx, notifications.Object, webhooks.Object, NullLogger<OverdueService>.Instance), notifications, webhooks);
    }

    private static async Task<(Guid ownerId, Guid memberId, Guid projectId)> SeedProjectWithMemberAsync(TaskpilotDbContext ctx)
    {
        var ownerId = await TestDb.AddUserAsync(ctx, "Owner");
        var memberId = await TestDb.AddUserAsync(ctx, "Member");
        var projectId = await TestDb.AddProjectAsync(ctx, ownerId);
        ctx.ProjectMembers.Add(new ProjectMember { Id = Guid.NewGuid(), ProjectId = projectId, UserId = memberId });
        await ctx.SaveChangesAsync();
        return (ownerId, memberId, projectId);
    }

    private static async Task<Guid> AddTaskAsync(TaskpilotDbContext ctx, Guid projectId, Guid creatorId, DateTime deadline)
    {
        var id = Guid.NewGuid();
        ctx.ProjectTasks.Add(new ProjectTask
        {
            Id = id,
            ProjectId = projectId,
            Title = "Ship it",
            Status = ProjectTaskStatus.InProgress,
            CreatorId = creatorId,
            Deadline = deadline,
        });
        await ctx.SaveChangesAsync();
        return id;
    }

    [Fact]
    public async Task Escalates_TaskOverduePastThreshold()
    {
        await using var ctx = TestDb.CreateContext();
        var (ownerId, memberId, projectId) = await SeedProjectWithMemberAsync(ctx);
        var taskId = await AddTaskAsync(ctx, projectId, ownerId, DateTime.UtcNow.AddDays(-4)); // 4 days overdue
        var (svc, notifications, webhooks) = Create(ctx);

        await svc.ProcessOverdueAsync();

        // The escalation webhook fired once.
        webhooks.Verify(w => w.DispatchAsync(WebhookEvents.EscalationTriggered, It.IsAny<object>()), Times.Once);
        // Owner and the member were both notified about the escalation.
        notifications.Verify(n => n.CreateAsync(ownerId, NotificationType.Task, It.Is<string>(s => s.Contains("Escalation")), It.IsAny<string?>()), Times.Once);
        notifications.Verify(n => n.CreateAsync(memberId, NotificationType.Task, It.Is<string>(s => s.Contains("Escalation")), It.IsAny<string?>()), Times.Once);
        // The task is stamped as escalated.
        var task = await ctx.ProjectTasks.FindAsync(taskId);
        Assert.NotNull(task!.EscalatedAt);
    }

    [Fact]
    public async Task DoesNotEscalate_TaskOverdueWithinThreshold()
    {
        await using var ctx = TestDb.CreateContext();
        var (ownerId, _, projectId) = await SeedProjectWithMemberAsync(ctx);
        await AddTaskAsync(ctx, projectId, ownerId, DateTime.UtcNow.AddDays(-1)); // only 1 day overdue
        var (svc, _, webhooks) = Create(ctx);

        await svc.ProcessOverdueAsync();

        webhooks.Verify(w => w.DispatchAsync(WebhookEvents.EscalationTriggered, It.IsAny<object>()), Times.Never);
    }

    [Fact]
    public async Task Escalates_OnlyOnce_AcrossRuns()
    {
        await using var ctx = TestDb.CreateContext();
        var (ownerId, _, projectId) = await SeedProjectWithMemberAsync(ctx);
        await AddTaskAsync(ctx, projectId, ownerId, DateTime.UtcNow.AddDays(-4));
        var (svc, _, webhooks) = Create(ctx);

        await svc.ProcessOverdueAsync();
        await svc.ProcessOverdueAsync(); // second run must be a no-op for escalation

        webhooks.Verify(w => w.DispatchAsync(WebhookEvents.EscalationTriggered, It.IsAny<object>()), Times.Once);
    }

    [Fact]
    public async Task DoesNotEscalate_DoneTask()
    {
        await using var ctx = TestDb.CreateContext();
        var (ownerId, _, projectId) = await SeedProjectWithMemberAsync(ctx);
        var id = Guid.NewGuid();
        ctx.ProjectTasks.Add(new ProjectTask
        {
            Id = id,
            ProjectId = projectId,
            Title = "Finished",
            Status = ProjectTaskStatus.Done,
            CreatorId = ownerId,
            Deadline = DateTime.UtcNow.AddDays(-5),
        });
        await ctx.SaveChangesAsync();
        var (svc, _, webhooks) = Create(ctx);

        await svc.ProcessOverdueAsync();

        webhooks.Verify(w => w.DispatchAsync(WebhookEvents.EscalationTriggered, It.IsAny<object>()), Times.Never);
    }
}
