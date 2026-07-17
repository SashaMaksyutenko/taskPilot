using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Taskpilot.API.Common;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Projects;
using Taskpilot.API.Models;
using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>
/// Tests for the task history (audit trail): that mutating a task records what changed,
/// and that <see cref="TaskService.GetHistoryAsync"/> reads it back safely.
/// These wire a REAL <see cref="AuditService"/> rather than a mock, so each test covers
/// the whole write-then-read path over the in-memory database.
/// </summary>
public class TaskAuditTests
{
    private static TaskService Create(TaskpilotDbContext ctx) =>
        new(ctx,
            new Mock<IWebhookService>().Object,
            new Mock<INotificationService>().Object,
            new Mock<IReputationService>().Object,
            new AuditService(ctx, NullLogger<AuditService>.Instance),
            NullLogger<TaskService>.Instance);

    /// <summary>Seeds an owner with a project and one task, and returns the service under test.</summary>
    private static async Task<(TaskService svc, Guid owner, Guid projectId, TaskDto task)> SetupAsync(
        TaskpilotDbContext ctx, string title = "Ship")
    {
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var projectId = await TestDb.AddProjectAsync(ctx, owner);
        var svc = Create(ctx);
        var task = (await svc.CreateTaskAsync(owner, projectId, new CreateTaskDto { Title = title })).Value!;
        return (svc, owner, projectId, task);
    }

    [Fact]
    public async Task CreateTask_OpensTheHistory()
    {
        using var ctx = TestDb.CreateContext();
        var (svc, owner, _, task) = await SetupAsync(ctx);

        var history = (await svc.GetHistoryAsync(owner, task.Id)).Value!;

        var entry = Assert.Single(history);
        Assert.Equal(TaskAuditActions.Created, entry.Action);
        Assert.Equal(owner, entry.ActorId);
        Assert.Equal("Owner", entry.ActorName);
        Assert.Contains("Ship", entry.Details);
    }

    [Fact]
    public async Task ChangeStatus_RecordsTheTransition()
    {
        using var ctx = TestDb.CreateContext();
        var (svc, owner, _, task) = await SetupAsync(ctx);

        await svc.ChangeStatusAsync(owner, task.Id, "InProgress");

        var history = (await svc.GetHistoryAsync(owner, task.Id)).Value!;
        // Newest first, so the status change leads and the creation trails it.
        Assert.Equal(2, history.Count);
        Assert.Equal(TaskAuditActions.StatusChanged, history[0].Action);
        Assert.Equal("Status: Backlog → InProgress", history[0].Details);
        Assert.Equal(TaskAuditActions.Created, history[1].Action);
    }

    [Fact]
    public async Task ChangeStatus_ToTheSameStatus_RecordsNothing()
    {
        using var ctx = TestDb.CreateContext();
        var (svc, owner, _, task) = await SetupAsync(ctx);

        // The task is already in Backlog — a drag onto its own column must not add noise.
        await svc.ChangeStatusAsync(owner, task.Id, "Backlog");

        var history = (await svc.GetHistoryAsync(owner, task.Id)).Value!;
        Assert.Single(history);   // only the creation entry
    }

    [Fact]
    public async Task UpdateTask_RecordsOnlyTheChangedFields()
    {
        using var ctx = TestDb.CreateContext();
        var (svc, owner, _, task) = await SetupAsync(ctx);

        await svc.UpdateTaskAsync(owner, task.Id, new UpdateTaskDto
        {
            Title = "Ship it",           // changed
            Priority = "High",           // changed (was Medium)
        });

        var history = (await svc.GetHistoryAsync(owner, task.Id)).Value!;
        var entry = history[0];
        Assert.Equal(TaskAuditActions.Updated, entry.Action);
        Assert.Contains("Title: \"Ship\" → \"Ship it\"", entry.Details);
        Assert.Contains("Priority: Medium → High", entry.Details);
        // Nothing else moved, so nothing else is reported.
        Assert.DoesNotContain("Deadline", entry.Details);
        Assert.DoesNotContain("Assignee", entry.Details);
    }

    [Fact]
    public async Task UpdateTask_WithNoRealChange_RecordsNothing()
    {
        using var ctx = TestDb.CreateContext();
        var (svc, owner, _, task) = await SetupAsync(ctx);

        // Re-submitting the same values (as an unchanged edit form would) is not history.
        await svc.UpdateTaskAsync(owner, task.Id, new UpdateTaskDto { Title = "Ship", Priority = "Medium" });

        var history = (await svc.GetHistoryAsync(owner, task.Id)).Value!;
        Assert.Single(history);   // only the creation entry
    }

    [Fact]
    public async Task DeleteTask_LeavesTheHistoryBehind()
    {
        using var ctx = TestDb.CreateContext();
        var (svc, owner, _, task) = await SetupAsync(ctx);

        await svc.DeleteTaskAsync(owner, task.Id);

        // The task row is gone...
        Assert.False(await ctx.ProjectTasks.AnyAsync(t => t.Id == task.Id));
        // ...but the trail survives it, because AuditLog has no foreign key to the task.
        var entries = await ctx.AuditLogs
            .Where(a => a.EntityType == nameof(ProjectTask) && a.EntityId == task.Id.ToString())
            .ToListAsync();
        Assert.Contains(entries, e => e.Action == TaskAuditActions.Deleted && e.Details!.Contains("Ship"));
    }

    [Fact]
    public async Task GetHistory_NeverExposesTheActorsEmailOrIp()
    {
        using var ctx = TestDb.CreateContext();
        var (svc, owner, _, task) = await SetupAsync(ctx);

        var history = (await svc.GetHistoryAsync(owner, task.Id)).Value!;

        // The stored entry keeps the email snapshot for admins...
        var stored = await ctx.AuditLogs.FirstAsync(a => a.EntityId == task.Id.ToString());
        Assert.NotNull(stored.ActorEmail);
        // ...but the DTO handed to teammates carries a name only.
        Assert.Equal("Owner", history[0].ActorName);
        Assert.DoesNotContain(typeof(TaskHistoryEntryDto).GetProperties(), p => p.Name is "ActorEmail" or "IpAddress");
    }

    [Fact]
    public async Task GetHistory_IsReadableByAProjectMember_ButNotByAnOutsider()
    {
        using var ctx = TestDb.CreateContext();
        var (svc, _, projectId, task) = await SetupAsync(ctx);

        // A Viewer member may read the history: it is a read, not an edit.
        var viewer = await TestDb.AddUserAsync(ctx, "Viewer");
        ctx.ProjectMembers.Add(new ProjectMember
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            UserId = viewer,
            Role = ProjectMemberRole.Viewer,
        });
        await ctx.SaveChangesAsync();

        var asMember = await svc.GetHistoryAsync(viewer, task.Id);
        Assert.True(asMember.Succeeded);
        Assert.Single(asMember.Value!);

        // Someone with no access to the project cannot read it at all.
        var outsider = await TestDb.AddUserAsync(ctx, "Outsider");
        var asOutsider = await svc.GetHistoryAsync(outsider, task.Id);
        Assert.False(asOutsider.Succeeded);
        Assert.Equal("Task not found.", asOutsider.Error);
    }
}
