using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Taskpilot.API.Data;
using Taskpilot.API.Models;
using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>
/// Tests for <see cref="TaskService.GetProjectTeamWorkloadAsync"/>: the team-availability
/// read that groups a project's assigned, in-range tasks by participant.
/// </summary>
public class TeamWorkloadTests
{
    private static TaskService Create(TaskpilotDbContext ctx) =>
        new(ctx,
            new Mock<IWebhookService>().Object,
            new Mock<INotificationService>().Object,
            new Mock<IReputationService>().Object,
            new Mock<IAuditService>().Object,
            NullLogger<TaskService>.Instance);

    private static readonly DateTime From = new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime To = new(2026, 7, 31, 23, 59, 59, DateTimeKind.Utc);

    /// <summary>Adds a member to a project.</summary>
    private static async Task AddMemberAsync(TaskpilotDbContext ctx, Guid projectId, Guid userId, ProjectMemberRole role = ProjectMemberRole.Editor)
    {
        ctx.ProjectMembers.Add(new ProjectMember
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            UserId = userId,
            Role = role,
        });
        await ctx.SaveChangesAsync();
    }

    /// <summary>Adds a task with the given assignee and deadline.</summary>
    private static async Task AddTaskAsync(
        TaskpilotDbContext ctx, Guid projectId, Guid? assigneeId, Guid creatorId, DateTime? deadline, string title)
    {
        ctx.ProjectTasks.Add(new ProjectTask
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Title = title,
            Status = ProjectTaskStatus.Backlog,
            Priority = TaskPriority.Medium,
            AssigneeId = assigneeId,
            CreatorId = creatorId,
            Deadline = deadline,
            CreatedAt = DateTime.UtcNow,
            Tags = new List<string>(),
        });
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task GroupsInRangeTasksByAssignee_AndIncludesEveryParticipant()
    {
        using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var alice = await TestDb.AddUserAsync(ctx, "Alice");
        var bob = await TestDb.AddUserAsync(ctx, "Bob");
        var projectId = await TestDb.AddProjectAsync(ctx, owner);
        await AddMemberAsync(ctx, projectId, alice);
        await AddMemberAsync(ctx, projectId, bob);

        await AddTaskAsync(ctx, projectId, owner, owner, From.AddDays(2), "Owner task");
        await AddTaskAsync(ctx, projectId, alice, owner, From.AddDays(5), "Alice task 1");
        await AddTaskAsync(ctx, projectId, alice, owner, From.AddDays(9), "Alice task 2");
        // Bob is assigned nothing — he should still appear, with an empty list (he is free).

        var svc = Create(ctx);
        var result = await svc.GetProjectTeamWorkloadAsync(owner, projectId, From, To);

        Assert.True(result.Succeeded);
        var team = result.Value!;
        Assert.Equal(3, team.Count);

        // Owner is listed first and flagged as owner.
        Assert.Equal("Owner", team[0].Name);
        Assert.True(team[0].IsOwner);
        Assert.Single(team[0].Tasks);

        var aliceRow = team.Single(m => m.Name == "Alice");
        Assert.Equal(2, aliceRow.Tasks.Count);
        // Ordered by deadline.
        Assert.Equal("Alice task 1", aliceRow.Tasks[0].Title);

        var bobRow = team.Single(m => m.Name == "Bob");
        Assert.Empty(bobRow.Tasks);
    }

    [Fact]
    public async Task ExcludesTasksOutsideTheDateRange()
    {
        using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var projectId = await TestDb.AddProjectAsync(ctx, owner);

        await AddTaskAsync(ctx, projectId, owner, owner, From.AddDays(3), "In range");
        await AddTaskAsync(ctx, projectId, owner, owner, To.AddDays(10), "After range");
        await AddTaskAsync(ctx, projectId, owner, owner, From.AddDays(-10), "Before range");

        var svc = Create(ctx);
        var team = (await svc.GetProjectTeamWorkloadAsync(owner, projectId, From, To)).Value!;

        var ownerRow = team.Single();
        Assert.Single(ownerRow.Tasks);
        Assert.Equal("In range", ownerRow.Tasks[0].Title);
    }

    [Fact]
    public async Task ExcludesUnassignedTasksAndTasksWithoutADeadline()
    {
        using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var projectId = await TestDb.AddProjectAsync(ctx, owner);

        await AddTaskAsync(ctx, projectId, assigneeId: null, owner, From.AddDays(3), "Unassigned");
        await AddTaskAsync(ctx, projectId, owner, owner, deadline: null, "No deadline");
        await AddTaskAsync(ctx, projectId, owner, owner, From.AddDays(4), "Counted");

        var svc = Create(ctx);
        var team = (await svc.GetProjectTeamWorkloadAsync(owner, projectId, From, To)).Value!;

        Assert.Single(team.Single().Tasks);
        Assert.Equal("Counted", team.Single().Tasks[0].Title);
    }

    [Fact]
    public async Task AViewerMemberCanReadTheWorkload()
    {
        using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var viewer = await TestDb.AddUserAsync(ctx, "Viewer");
        var projectId = await TestDb.AddProjectAsync(ctx, owner);
        await AddMemberAsync(ctx, projectId, viewer, ProjectMemberRole.Viewer);

        var svc = Create(ctx);
        var result = await svc.GetProjectTeamWorkloadAsync(viewer, projectId, From, To);

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.Value!.Count);   // owner + viewer
    }

    [Fact]
    public async Task AnOutsiderGets404()
    {
        using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var outsider = await TestDb.AddUserAsync(ctx, "Outsider");
        var projectId = await TestDb.AddProjectAsync(ctx, owner);

        var svc = Create(ctx);
        var result = await svc.GetProjectTeamWorkloadAsync(outsider, projectId, From, To);

        Assert.False(result.Succeeded);
        Assert.Equal("Project not found.", result.Error);
    }
}
