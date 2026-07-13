using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Projects;
using Taskpilot.API.Models;
using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>Unit tests for <see cref="ExtensionService"/> (deadline-extension requests).</summary>
public class ExtensionServiceTests
{
    private static ExtensionService Create(TaskpilotDbContext ctx)
    {
        var notifications = new Mock<INotificationService>();
        notifications
            .Setup(n => n.CreateAsync(It.IsAny<Guid>(), It.IsAny<NotificationType>(), It.IsAny<string>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);
        return new ExtensionService(ctx, notifications.Object, NullLogger<ExtensionService>.Instance);
    }

    private static async Task<(Guid ownerId, Guid memberId, Guid taskId)> SeedAsync(TaskpilotDbContext ctx)
    {
        var ownerId = await TestDb.AddUserAsync(ctx, "Owner");
        var memberId = await TestDb.AddUserAsync(ctx, "Member");
        var projectId = await TestDb.AddProjectAsync(ctx, ownerId);
        ctx.ProjectMembers.Add(new ProjectMember { Id = Guid.NewGuid(), ProjectId = projectId, UserId = memberId });
        var taskId = Guid.NewGuid();
        ctx.ProjectTasks.Add(new ProjectTask
        {
            Id = taskId,
            ProjectId = projectId,
            Title = "Ship",
            CreatorId = ownerId,
            AssigneeId = memberId,
            Deadline = DateTime.UtcNow.AddDays(1),
            OverdueNotifiedAt = DateTime.UtcNow,
            EscalatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();
        return (ownerId, memberId, taskId);
    }

    private static CreateExtensionRequestDto Dto(int daysFromNow = 7) =>
        new() { RequestedDeadline = DateTime.UtcNow.AddDays(daysFromNow), Reason = "Need more time" };

    [Fact]
    public async Task Request_CreatesPending()
    {
        await using var ctx = TestDb.CreateContext();
        var (_, member, taskId) = await SeedAsync(ctx);

        var result = await Create(ctx).RequestAsync(member, taskId, Dto());

        Assert.True(result.Succeeded);
        Assert.Equal("Pending", result.Value!.Status);
        Assert.Equal(1, await ctx.TaskExtensionRequests.CountAsync());
    }

    [Fact]
    public async Task Request_PastDeadline_Fails()
    {
        await using var ctx = TestDb.CreateContext();
        var (_, member, taskId) = await SeedAsync(ctx);

        var result = await Create(ctx).RequestAsync(member, taskId, Dto(-1));

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Request_SecondPending_Fails()
    {
        await using var ctx = TestDb.CreateContext();
        var (_, member, taskId) = await SeedAsync(ctx);
        var svc = Create(ctx);

        Assert.True((await svc.RequestAsync(member, taskId, Dto())).Succeeded);
        Assert.False((await svc.RequestAsync(member, taskId, Dto())).Succeeded);
    }

    [Fact]
    public async Task Request_NonMember_Fails()
    {
        await using var ctx = TestDb.CreateContext();
        var (_, _, taskId) = await SeedAsync(ctx);
        var outsider = await TestDb.AddUserAsync(ctx, "Outsider");

        var result = await Create(ctx).RequestAsync(outsider, taskId, Dto());

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Decide_Approve_MovesDeadlineAndClearsFlags()
    {
        await using var ctx = TestDb.CreateContext();
        var (owner, member, taskId) = await SeedAsync(ctx);
        var svc = Create(ctx);
        var newDeadline = DateTime.UtcNow.AddDays(7);
        var req = (await svc.RequestAsync(member, taskId, new CreateExtensionRequestDto { RequestedDeadline = newDeadline, Reason = "x" })).Value!;

        var result = await svc.DecideAsync(owner, req.Id, approve: true);

        Assert.True(result.Succeeded);
        Assert.Equal("Approved", result.Value!.Status);
        var task = await ctx.ProjectTasks.FindAsync(taskId);
        Assert.Equal(newDeadline, task!.Deadline);
        Assert.Null(task.OverdueNotifiedAt);
        Assert.Null(task.EscalatedAt);
    }

    [Fact]
    public async Task Decide_Reject_LeavesDeadline()
    {
        await using var ctx = TestDb.CreateContext();
        var (owner, member, taskId) = await SeedAsync(ctx);
        var svc = Create(ctx);
        var original = (await ctx.ProjectTasks.FindAsync(taskId))!.Deadline;
        var req = (await svc.RequestAsync(member, taskId, Dto())).Value!;

        var result = await svc.DecideAsync(owner, req.Id, approve: false);

        Assert.True(result.Succeeded);
        Assert.Equal("Rejected", result.Value!.Status);
        ctx.ChangeTracker.Clear();
        var task = await ctx.ProjectTasks.FindAsync(taskId);
        Assert.Equal(original, task!.Deadline);
    }

    [Fact]
    public async Task Decide_NonOwner_Fails()
    {
        await using var ctx = TestDb.CreateContext();
        var (_, member, taskId) = await SeedAsync(ctx);
        var svc = Create(ctx);
        var req = (await svc.RequestAsync(member, taskId, Dto())).Value!;

        // The member (requester) is not the owner and cannot decide.
        var result = await svc.DecideAsync(member, req.Id, approve: true);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task GetForTask_OwnerCanDecidePending()
    {
        await using var ctx = TestDb.CreateContext();
        var (owner, member, taskId) = await SeedAsync(ctx);
        var svc = Create(ctx);
        await svc.RequestAsync(member, taskId, Dto());

        var ownerView = await svc.GetForTaskAsync(owner, taskId);
        var memberView = await svc.GetForTaskAsync(member, taskId);

        Assert.True(ownerView.Value!.Single().CanDecide);
        Assert.False(memberView.Value!.Single().CanDecide);
    }
}
