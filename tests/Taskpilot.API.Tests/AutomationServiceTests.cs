using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Automations;
using Taskpilot.API.Models;
using Taskpilot.API.Services;
using Taskpilot.Contracts;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>
/// Tests for project automations: owner-guarded CRUD/validation and the rule engine that runs
/// actions when a task is created or its status changes.
/// </summary>
public class AutomationServiceTests
{
    private static AutomationService Make(TaskpilotDbContext ctx, Mock<INotificationService>? notifications = null)
        => new(ctx, (notifications ?? new Mock<INotificationService>()).Object, NullLogger<AutomationService>.Instance);

    private static async Task<ProjectTask> SeedTaskAsync(
        TaskpilotDbContext ctx, Guid owner, Guid projectId,
        ProjectTaskStatus status = ProjectTaskStatus.Backlog, TaskPriority priority = TaskPriority.Low)
    {
        var task = new ProjectTask { Id = Guid.NewGuid(), ProjectId = projectId, CreatorId = owner, Title = "T", Status = status, Priority = priority };
        ctx.ProjectTasks.Add(task);
        await ctx.SaveChangesAsync();
        return task;
    }

    private static async Task AddRuleAsync(TaskpilotDbContext ctx, Guid projectId, AutomationTrigger trigger,
        AutomationAction action, string? value, ProjectTaskStatus? triggerStatus = null, bool enabled = true)
    {
        ctx.AutomationRules.Add(new AutomationRule
        {
            Id = Guid.NewGuid(), ProjectId = projectId, Name = "Rule", IsEnabled = enabled,
            Trigger = trigger, TriggerStatus = triggerStatus, Action = action, ActionValue = value,
        });
        await ctx.SaveChangesAsync();
    }

    // --- CRUD / validation ---

    [Fact]
    public async Task CreateRule_ByNonOwner_Fails()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var outsider = await TestDb.AddUserAsync(ctx, "Outsider");
        var project = await TestDb.AddProjectAsync(ctx, owner, "P");

        var result = await Make(ctx).CreateRuleAsync(outsider, project, new SaveAutomationRuleDto
        {
            Trigger = "OnTaskCreated", Action = "NotifyOwner",
        });

        Assert.False(result.Succeeded);
        Assert.Empty(ctx.AutomationRules);
    }

    [Fact]
    public async Task CreateRule_ByOwner_Succeeds_AndDefaultsName()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var project = await TestDb.AddProjectAsync(ctx, owner, "P");

        var result = await Make(ctx).CreateRuleAsync(owner, project, new SaveAutomationRuleDto
        {
            Trigger = "OnTaskStatusChanged", TriggerStatus = "Done", Action = "NotifyOwner", Name = "",
        });

        Assert.True(result.Succeeded);
        Assert.Equal("OnTaskStatusChanged", result.Value!.Trigger);
        Assert.Equal("Done", result.Value.TriggerStatus);
        Assert.False(string.IsNullOrWhiteSpace(result.Value.Name)); // a default label was generated
        Assert.Single(ctx.AutomationRules);
    }

    [Fact]
    public async Task CreateRule_SetPriority_InvalidValue_Fails()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var project = await TestDb.AddProjectAsync(ctx, owner, "P");

        var result = await Make(ctx).CreateRuleAsync(owner, project, new SaveAutomationRuleDto
        {
            Trigger = "OnTaskCreated", Action = "SetPriority", ActionValue = "Urgent",
        });

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task CreateRule_AssignToNonMember_Fails()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var stranger = await TestDb.AddUserAsync(ctx, "Stranger");
        var project = await TestDb.AddProjectAsync(ctx, owner, "P");

        var result = await Make(ctx).CreateRuleAsync(owner, project, new SaveAutomationRuleDto
        {
            Trigger = "OnTaskCreated", Action = "AssignToUser", ActionValue = stranger.ToString(),
        });

        Assert.False(result.Succeeded);
    }

    // --- engine ---

    [Fact]
    public async Task StatusChanged_SetPriority_AppliesToTask()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var project = await TestDb.AddProjectAsync(ctx, owner, "P");
        await AddRuleAsync(ctx, project, AutomationTrigger.OnTaskStatusChanged, AutomationAction.SetPriority, "High");
        var task = await SeedTaskAsync(ctx, owner, project, ProjectTaskStatus.Review);

        await Make(ctx).RunOnTaskStatusChangedAsync(task);

        Assert.Equal(TaskPriority.High, task.Priority);
    }

    [Fact]
    public async Task StatusChanged_StatusFilter_OnlyFiresForMatchingStatus()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var project = await TestDb.AddProjectAsync(ctx, owner, "P");
        await AddRuleAsync(ctx, project, AutomationTrigger.OnTaskStatusChanged, AutomationAction.SetPriority, "High",
            triggerStatus: ProjectTaskStatus.Done);
        var svc = Make(ctx);

        var review = await SeedTaskAsync(ctx, owner, project, ProjectTaskStatus.Review);
        await svc.RunOnTaskStatusChangedAsync(review);
        Assert.Equal(TaskPriority.Low, review.Priority); // not Done → rule skipped

        var done = await SeedTaskAsync(ctx, owner, project, ProjectTaskStatus.Done);
        await svc.RunOnTaskStatusChangedAsync(done);
        Assert.Equal(TaskPriority.High, done.Priority); // Done → rule fired
    }

    [Fact]
    public async Task TaskCreated_AssignToMember_Assigns()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var member = await TestDb.AddUserAsync(ctx, "Member");
        var project = await TestDb.AddProjectAsync(ctx, owner, "P");
        ctx.ProjectMembers.Add(new ProjectMember { Id = Guid.NewGuid(), ProjectId = project, UserId = member });
        await ctx.SaveChangesAsync();
        await AddRuleAsync(ctx, project, AutomationTrigger.OnTaskCreated, AutomationAction.AssignToUser, member.ToString());
        var task = await SeedTaskAsync(ctx, owner, project);

        await Make(ctx).RunOnTaskCreatedAsync(task);

        Assert.Equal(member, task.AssigneeId);
    }

    [Fact]
    public async Task StatusChanged_NotifyOwner_SendsNotification()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var project = await TestDb.AddProjectAsync(ctx, owner, "P");
        await AddRuleAsync(ctx, project, AutomationTrigger.OnTaskStatusChanged, AutomationAction.NotifyOwner, null);
        var task = await SeedTaskAsync(ctx, owner, project, ProjectTaskStatus.Done);
        var notifications = new Mock<INotificationService>();

        await Make(ctx, notifications).RunOnTaskStatusChangedAsync(task);

        notifications.Verify(n => n.CreateAsync(owner, NotificationType.Task, It.IsAny<string>(), It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task StatusChanged_AddComment_AddsCommentAuthoredByOwner()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var project = await TestDb.AddProjectAsync(ctx, owner, "P");
        await AddRuleAsync(ctx, project, AutomationTrigger.OnTaskStatusChanged, AutomationAction.AddComment, "Auto note");
        var task = await SeedTaskAsync(ctx, owner, project, ProjectTaskStatus.Done);

        await Make(ctx).RunOnTaskStatusChangedAsync(task);

        var comment = Assert.Single(ctx.TaskComments.Where(c => c.TaskId == task.Id));
        Assert.Equal("Auto note", comment.Body);
        Assert.Equal(owner, comment.AuthorId);
    }

    [Fact]
    public async Task DisabledRule_DoesNotFire()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var project = await TestDb.AddProjectAsync(ctx, owner, "P");
        await AddRuleAsync(ctx, project, AutomationTrigger.OnTaskStatusChanged, AutomationAction.SetPriority, "High", enabled: false);
        var task = await SeedTaskAsync(ctx, owner, project, ProjectTaskStatus.Done);

        await Make(ctx).RunOnTaskStatusChangedAsync(task);

        Assert.Equal(TaskPriority.Low, task.Priority);
    }
}
