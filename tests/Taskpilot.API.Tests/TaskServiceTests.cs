using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Projects;
using Taskpilot.API.Models;
using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>Unit tests for <see cref="TaskService"/> over an in-memory database.</summary>
public class TaskServiceTests
{
    private static TaskService Create(TaskpilotDbContext ctx) => CreateWithMock(ctx).svc;

    private static (TaskService svc, Mock<INotificationService> notifications) CreateWithMock(TaskpilotDbContext ctx)
    {
        var notifications = new Mock<INotificationService>();
        var svc = new TaskService(ctx, new Mock<IWebhookService>().Object, notifications.Object, new Mock<IReputationService>().Object, new Mock<IAuditService>().Object, new Mock<ITaskAttachmentService>().Object, new Mock<IAutomationService>().Object, NullLogger<TaskService>.Instance);
        return (svc, notifications);
    }

    [Fact]
    public async Task Reschedule_MovesOnlyTheDeadline()
    {
        using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx);
        var projectId = await TestDb.AddProjectAsync(ctx, owner);
        var svc = Create(ctx);
        var created = (await svc.CreateTaskAsync(owner, projectId, new CreateTaskDto
        {
            Title = "Ship",
            Description = "Keep me",
            AssigneeId = owner,
            Deadline = DateTime.UtcNow.AddDays(1),
            Tags = new List<string> { "keep" },
        })).Value!;

        var newDeadline = DateTime.UtcNow.AddDays(5);
        var result = await svc.RescheduleAsync(owner, created.Id, newDeadline);

        Assert.True(result.Succeeded);
        // Only the deadline moved — the other fields survive.
        var task = await ctx.ProjectTasks.FirstAsync(x => x.Id == created.Id);
        Assert.Equal(newDeadline, task.Deadline);
        Assert.Equal("Keep me", task.Description);
        Assert.Equal(owner, task.AssigneeId);
        Assert.Contains("keep", task.Tags);
    }

    [Fact]
    public async Task Reschedule_ClearsOverdueAndEscalationFlags()
    {
        using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx);
        var projectId = await TestDb.AddProjectAsync(ctx, owner);
        var svc = Create(ctx);
        var created = (await svc.CreateTaskAsync(owner, projectId, new CreateTaskDto
        {
            Title = "Late",
            Deadline = DateTime.UtcNow.AddDays(-9),
        })).Value!;

        // Pretend the background check already flagged and escalated it.
        var task = await ctx.ProjectTasks.FirstAsync(x => x.Id == created.Id);
        task.OverdueNotifiedAt = DateTime.UtcNow;
        task.EscalatedAt = DateTime.UtcNow;
        task.EscalationLevel = 3;
        await ctx.SaveChangesAsync();

        await svc.RescheduleAsync(owner, created.Id, DateTime.UtcNow.AddDays(7));

        var moved = await ctx.ProjectTasks.FirstAsync(x => x.Id == created.Id);
        Assert.Null(moved.OverdueNotifiedAt);
        Assert.Null(moved.EscalatedAt);
        Assert.Equal(0, moved.EscalationLevel);
    }

    [Fact]
    public async Task Reschedule_NonMember_Fails()
    {
        using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var outsider = await TestDb.AddUserAsync(ctx, "Outsider");
        var projectId = await TestDb.AddProjectAsync(ctx, owner);
        var svc = Create(ctx);
        var created = (await svc.CreateTaskAsync(owner, projectId, new CreateTaskDto { Title = "T" })).Value!;

        var result = await svc.RescheduleAsync(outsider, created.Id, DateTime.UtcNow.AddDays(3));

        Assert.False(result.Succeeded);
    }

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

    [Fact]
    public async Task ChangeStatus_NotifiesAssignee_WhenMovedByAnother()
    {
        using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var assignee = await TestDb.AddUserAsync(ctx, "Assignee");
        var projectId = await TestDb.AddProjectAsync(ctx, owner);
        var (svc, notifications) = CreateWithMock(ctx);
        var task = await svc.CreateTaskAsync(owner, projectId, new CreateTaskDto { Title = "Task", AssigneeId = assignee });
        notifications.Invocations.Clear(); // ignore the "assigned" notification from creation

        await svc.ChangeStatusAsync(owner, task.Value!.Id, "InProgress");

        notifications.Verify(n => n.CreateAsync(assignee, It.IsAny<NotificationType>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task ChangeStatus_ToDone_NotifiesCreator_WhenCompletedByAnother()
    {
        using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var member = await TestDb.AddUserAsync(ctx, "Member");
        var projectId = await TestDb.AddProjectAsync(ctx, owner);
        // The member is an Editor, so they can create tasks in the project.
        ctx.ProjectMembers.Add(new ProjectMember
        {
            Id = Guid.NewGuid(), ProjectId = projectId, UserId = member, Role = ProjectMemberRole.Editor,
        });
        await ctx.SaveChangesAsync();
        var (svc, notifications) = CreateWithMock(ctx);
        // The member creates the task, so they are its creator.
        var task = await svc.CreateTaskAsync(member, projectId, new CreateTaskDto { Title = "Task" });
        notifications.Invocations.Clear();

        // Only the owner may move a task to Done; doing so completes the member's task.
        var result = await svc.ChangeStatusAsync(owner, task.Value!.Id, "Done");

        Assert.True(result.Succeeded);
        // The creator (member) is told it's done; the owner (actor) is not self-notified.
        notifications.Verify(n => n.CreateAsync(member, It.IsAny<NotificationType>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        notifications.Verify(n => n.CreateAsync(owner, It.IsAny<NotificationType>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Recurring_Weekly_SpawnsNextOccurrenceOnDone_AndStopsRecurringItself()
    {
        using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx);
        var projectId = await TestDb.AddProjectAsync(ctx, owner);
        var svc = Create(ctx);
        var deadline = new DateTime(2026, 8, 3, 9, 0, 0, DateTimeKind.Utc);
        var created = (await svc.CreateTaskAsync(owner, projectId, new CreateTaskDto
        {
            Title = "Weekly report", AssigneeId = owner, Deadline = deadline,
            Recurrence = "Weekly", RecurrenceInterval = 1, Tags = new List<string> { "ops" },
        })).Value!;

        Assert.True((await svc.ChangeStatusAsync(owner, created.Id, "Done")).Succeeded);

        var tasks = await ctx.ProjectTasks.Where(t => t.ProjectId == projectId).ToListAsync();
        Assert.Equal(2, tasks.Count);
        var original = tasks.Single(t => t.Id == created.Id);
        var next = tasks.Single(t => t.Id != created.Id);
        Assert.Equal(ProjectTaskStatus.Done, original.Status);
        Assert.Equal(RecurrenceType.None, original.RecurrenceType); // completed copy stops recurring
        Assert.Equal(ProjectTaskStatus.Backlog, next.Status);
        Assert.Equal(RecurrenceType.Weekly, next.RecurrenceType);
        Assert.Equal(deadline.AddDays(7), next.Deadline);
        Assert.Equal("Weekly report", next.Title);
        Assert.Contains("ops", next.Tags);
    }

    [Fact]
    public async Task NonRecurring_Done_DoesNotSpawn()
    {
        using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx);
        var projectId = await TestDb.AddProjectAsync(ctx, owner);
        var svc = Create(ctx);
        var created = (await svc.CreateTaskAsync(owner, projectId, new CreateTaskDto { Title = "One-off", AssigneeId = owner })).Value!;

        Assert.True((await svc.ChangeStatusAsync(owner, created.Id, "Done")).Succeeded);

        Assert.Equal(1, await ctx.ProjectTasks.CountAsync(t => t.ProjectId == projectId));
    }

    [Theory]
    [InlineData("Daily", 2, 2)]     // every 2 days -> +2 days
    [InlineData("Monthly", 1, 0)]   // every 1 month -> handled below (days arg ignored)
    public async Task Recurring_AdvancesDeadlineByRule(string recurrence, int interval, int addedDays)
    {
        using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx);
        var projectId = await TestDb.AddProjectAsync(ctx, owner);
        var svc = Create(ctx);
        var deadline = new DateTime(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc);
        var created = (await svc.CreateTaskAsync(owner, projectId, new CreateTaskDto
        {
            Title = "Repeat", AssigneeId = owner, Deadline = deadline, Recurrence = recurrence, RecurrenceInterval = interval,
        })).Value!;

        Assert.True((await svc.ChangeStatusAsync(owner, created.Id, "Done")).Succeeded);

        var next = await ctx.ProjectTasks.SingleAsync(t => t.ProjectId == projectId && t.Id != created.Id);
        var expected = recurrence == "Monthly" ? deadline.AddMonths(interval) : deadline.AddDays(addedDays);
        Assert.Equal(expected, next.Deadline);
    }

    [Fact]
    public async Task Recurring_NoDeadline_SchedulesNextFromCompletion()
    {
        using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx);
        var projectId = await TestDb.AddProjectAsync(ctx, owner);
        var svc = Create(ctx);
        var created = (await svc.CreateTaskAsync(owner, projectId, new CreateTaskDto
        {
            Title = "No deadline", AssigneeId = owner, Recurrence = "Weekly",
        })).Value!;

        var before = DateTime.UtcNow;
        Assert.True((await svc.ChangeStatusAsync(owner, created.Id, "Done")).Succeeded);

        var next = await ctx.ProjectTasks.SingleAsync(t => t.ProjectId == projectId && t.Id != created.Id);
        Assert.NotNull(next.Deadline);
        // ~7 days out from completion time.
        Assert.InRange(next.Deadline!.Value, before.AddDays(7).AddMinutes(-1), DateTime.UtcNow.AddDays(7).AddMinutes(1));
    }

    [Fact]
    public async Task Create_InvalidRecurrence_Fails()
    {
        using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx);
        var projectId = await TestDb.AddProjectAsync(ctx, owner);
        var svc = Create(ctx);

        var result = await svc.CreateTaskAsync(owner, projectId, new CreateTaskDto { Title = "X", Recurrence = "Yearly" });

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task ChangeStatus_ToDone_BlockedByUnfinishedDependency_FailsUntilBlockerDone()
    {
        using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx);
        var projectId = await TestDb.AddProjectAsync(ctx, owner);
        var svc = Create(ctx);
        var a = (await svc.CreateTaskAsync(owner, projectId, new CreateTaskDto { Title = "A", AssigneeId = owner })).Value!;
        var b = (await svc.CreateTaskAsync(owner, projectId, new CreateTaskDto { Title = "B", AssigneeId = owner })).Value!;
        ctx.TaskDependencies.Add(new TaskDependency { Id = Guid.NewGuid(), TaskId = a.Id, DependsOnTaskId = b.Id });
        await ctx.SaveChangesAsync();

        // A is blocked by B (still Backlog) — it cannot be completed yet.
        Assert.False((await svc.ChangeStatusAsync(owner, a.Id, "Done")).Succeeded);

        // Finish the blocker, then A can be completed.
        Assert.True((await svc.ChangeStatusAsync(owner, b.Id, "Done")).Succeeded);
        Assert.True((await svc.ChangeStatusAsync(owner, a.Id, "Done")).Succeeded);
    }

    [Fact]
    public async Task Estimate_SetOnCreate_ChangedAndClearedOnUpdate()
    {
        using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx);
        var projectId = await TestDb.AddProjectAsync(ctx, owner);
        var svc = Create(ctx);

        var created = (await svc.CreateTaskAsync(owner, projectId, new CreateTaskDto { Title = "Estimate me", Estimate = 5 })).Value!;
        Assert.Equal(5, created.Estimate);

        var raised = (await svc.UpdateTaskAsync(owner, created.Id, new UpdateTaskDto { Title = "Estimate me", Estimate = 8 })).Value!;
        Assert.Equal(8, raised.Estimate);

        // -1 clears the estimate; null would leave it unchanged.
        var cleared = (await svc.UpdateTaskAsync(owner, created.Id, new UpdateTaskDto { Title = "Estimate me", Estimate = -1 })).Value!;
        Assert.Null(cleared.Estimate);
    }

    [Fact]
    public async Task GetMyTasks_ReturnsAssignedInActiveProjects_OrderedByDeadline()
    {
        using var ctx = TestDb.CreateContext();
        var me = await TestDb.AddUserAsync(ctx, "Me");
        var other = await TestDb.AddUserAsync(ctx, "Other");
        var p1 = await TestDb.AddProjectAsync(ctx, me, "P1");
        var p2 = await TestDb.AddProjectAsync(ctx, me, "P2");
        var archived = await TestDb.AddProjectAsync(ctx, me, "Old");
        (await ctx.Projects.FindAsync(archived))!.ArchivedAt = DateTime.UtcNow;

        void Add(Guid project, Guid? assignee, DateTime? deadline, string title) => ctx.ProjectTasks.Add(new ProjectTask
        {
            Id = Guid.NewGuid(), ProjectId = project, CreatorId = me, Title = title, AssigneeId = assignee, Deadline = deadline,
        });
        Add(p1, me, DateTime.UtcNow.AddDays(2), "Later");
        Add(p2, me, DateTime.UtcNow.AddDays(1), "Sooner");
        Add(p1, me, null, "NoDeadline");
        Add(p1, other, DateTime.UtcNow.AddDays(1), "NotMine");   // assigned to someone else
        Add(archived, me, DateTime.UtcNow.AddDays(1), "Archived"); // in an archived project
        await ctx.SaveChangesAsync();

        var mine = (await Create(ctx).GetMyTasksAsync(me)).Value!;

        // Only my tasks in active projects; deadline ascending, no-deadline last.
        Assert.Equal(new[] { "Sooner", "Later", "NoDeadline" }, mine.Select(t => t.Title).ToArray());
        Assert.Contains(mine, t => t.Title == "Sooner" && t.ProjectName == "P2");
    }

    [Fact]
    public async Task ChangeStatus_NotifiesWatchers_ButNotTheActor()
    {
        using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var watcher = await TestDb.AddUserAsync(ctx, "Watcher");
        var projectId = await TestDb.AddProjectAsync(ctx, owner);
        ctx.ProjectMembers.Add(new ProjectMember { Id = Guid.NewGuid(), ProjectId = projectId, UserId = watcher });
        await ctx.SaveChangesAsync();

        var (svc, notifications) = CreateWithMock(ctx);
        var created = (await svc.CreateTaskAsync(owner, projectId, new CreateTaskDto { Title = "Watch me" })).Value!;
        // The member subscribes to the task.
        ctx.TaskWatchers.Add(new TaskWatcher { Id = Guid.NewGuid(), TaskId = created.Id, UserId = watcher });
        await ctx.SaveChangesAsync();

        var result = await svc.ChangeStatusAsync(owner, created.Id, "InProgress");
        Assert.True(result.Succeeded);

        // The watcher is notified; the owner (the actor) is not notified about their own move.
        notifications.Verify(n => n.CreateAsync(watcher, NotificationType.Task,
            It.Is<string>(s => s.Contains("watching")), It.IsAny<string?>()), Times.Once);
        notifications.Verify(n => n.CreateAsync(owner, It.IsAny<NotificationType>(),
            It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task ChangeStatus_RejectedWhenTargetColumnAtWipLimit()
    {
        using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx);
        var projectId = await TestDb.AddProjectAsync(ctx, owner);
        // InProgress may hold at most one task.
        ctx.ProjectWipLimits.Add(new ProjectWipLimit { Id = Guid.NewGuid(), ProjectId = projectId, Status = ProjectTaskStatus.InProgress, MaxTasks = 1 });
        await ctx.SaveChangesAsync();
        var svc = Create(ctx);

        var a = (await svc.CreateTaskAsync(owner, projectId, new CreateTaskDto { Title = "A" })).Value!;
        var b = (await svc.CreateTaskAsync(owner, projectId, new CreateTaskDto { Title = "B" })).Value!;

        // A moves in fine; B is rejected because the column is full.
        Assert.True((await svc.ChangeStatusAsync(owner, a.Id, "InProgress")).Succeeded);
        Assert.False((await svc.ChangeStatusAsync(owner, b.Id, "InProgress")).Succeeded);

        // Freeing the column lets B in.
        Assert.True((await svc.ChangeStatusAsync(owner, a.Id, "Review")).Succeeded);
        Assert.True((await svc.ChangeStatusAsync(owner, b.Id, "InProgress")).Succeeded);
    }
}
