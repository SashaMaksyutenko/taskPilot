using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Projects;
using Taskpilot.API.Models;
using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>
/// Tests the tightened task permissions (user feedback 2026-07-25): a non-owner Editor may
/// only touch tasks assigned to them (#6); moving a task to Review/Done is owner-only (#5);
/// changing a deadline is owner-only (#8b). The project owner can do anything.
/// </summary>
public class TaskPermissionTests
{
    private static TaskService Create(TaskpilotDbContext ctx) =>
        new(ctx, Mock.Of<IWebhookService>(), Mock.Of<INotificationService>(), Mock.Of<IReputationService>(),
            Mock.Of<IAuditService>(), Mock.Of<ITaskAttachmentService>(), NullLogger<TaskService>.Instance);

    /// <summary>Adds an Editor member to the project and returns their id.</summary>
    private static async Task<Guid> AddEditorAsync(TaskpilotDbContext ctx, Guid projectId, string name)
    {
        var id = await TestDb.AddUserAsync(ctx, name);
        ctx.ProjectMembers.Add(new ProjectMember
        {
            Id = Guid.NewGuid(), ProjectId = projectId, UserId = id, Role = ProjectMemberRole.Editor,
        });
        await ctx.SaveChangesAsync();
        return id;
    }

    [Fact]
    public async Task Editor_CannotChangeStatusOfATaskNotAssignedToThem()
    {
        using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var projectId = await TestDb.AddProjectAsync(ctx, owner);
        var editor = await AddEditorAsync(ctx, projectId, "Editor");
        var svc = Create(ctx);
        // A task assigned to the owner, not the editor.
        var task = (await svc.CreateTaskAsync(owner, projectId, new CreateTaskDto { Title = "T", AssigneeId = owner })).Value!;

        var result = await svc.ChangeStatusAsync(editor, task.Id, "InProgress");

        Assert.False(result.Succeeded);
        Assert.Equal("You can only change tasks assigned to you.", result.Error);
    }

    [Fact]
    public async Task Editor_CanMoveTheirOwnTask_BetweenNonReviewColumns()
    {
        using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var projectId = await TestDb.AddProjectAsync(ctx, owner);
        var editor = await AddEditorAsync(ctx, projectId, "Editor");
        var svc = Create(ctx);
        var task = (await svc.CreateTaskAsync(owner, projectId, new CreateTaskDto { Title = "T", AssigneeId = editor })).Value!;

        var result = await svc.ChangeStatusAsync(editor, task.Id, "InProgress");

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task Editor_CanSubmitTheirOwnTaskToReview_ButOnlyOwnerMovesItToDone()
    {
        using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var projectId = await TestDb.AddProjectAsync(ctx, owner);
        var editor = await AddEditorAsync(ctx, projectId, "Editor");
        var svc = Create(ctx);
        var task = (await svc.CreateTaskAsync(owner, projectId, new CreateTaskDto { Title = "T", AssigneeId = editor })).Value!;

        // The assignee submits their task for review...
        var toReview = await svc.ChangeStatusAsync(editor, task.Id, "Review");
        Assert.True(toReview.Succeeded);

        // ...but cannot approve it into Done — that's the owner's call.
        var toDone = await svc.ChangeStatusAsync(editor, task.Id, "Done");
        Assert.False(toDone.Succeeded);
        Assert.Equal("Only the project owner can move a task to Done.", toDone.Error);
    }

    [Fact]
    public async Task Owner_CanMoveAnyTaskToDone()
    {
        using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var projectId = await TestDb.AddProjectAsync(ctx, owner);
        var editor = await AddEditorAsync(ctx, projectId, "Editor");
        var svc = Create(ctx);
        var task = (await svc.CreateTaskAsync(owner, projectId, new CreateTaskDto { Title = "T", AssigneeId = editor })).Value!;

        var result = await svc.ChangeStatusAsync(owner, task.Id, "Done");

        Assert.True(result.Succeeded);
        Assert.Equal("Done", result.Value!.Status);
    }

    [Fact]
    public async Task Editor_CannotChangeTheDeadline_ButCanEditOtherFields()
    {
        using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var projectId = await TestDb.AddProjectAsync(ctx, owner);
        var editor = await AddEditorAsync(ctx, projectId, "Editor");
        var svc = Create(ctx);
        var d1 = DateTime.UtcNow.AddDays(1);
        var task = (await svc.CreateTaskAsync(owner, projectId, new CreateTaskDto { Title = "T", AssigneeId = editor, Deadline = d1 })).Value!;

        // Changing the deadline is refused...
        var changeDeadline = await svc.UpdateTaskAsync(editor, task.Id,
            new UpdateTaskDto { Title = "T", AssigneeId = editor, Deadline = DateTime.UtcNow.AddDays(9) });
        Assert.False(changeDeadline.Succeeded);
        Assert.Equal("Only the project owner can change the deadline.", changeDeadline.Error);

        // ...but editing the title (deadline unchanged) is allowed.
        var editTitle = await svc.UpdateTaskAsync(editor, task.Id,
            new UpdateTaskDto { Title = "Renamed", AssigneeId = editor, Deadline = d1 });
        Assert.True(editTitle.Succeeded);
        Assert.Equal("Renamed", editTitle.Value!.Title);
    }

    [Fact]
    public async Task Reschedule_ByANonOwner_IsRefused()
    {
        using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var projectId = await TestDb.AddProjectAsync(ctx, owner);
        var editor = await AddEditorAsync(ctx, projectId, "Editor");
        var svc = Create(ctx);
        var task = (await svc.CreateTaskAsync(owner, projectId, new CreateTaskDto { Title = "T", AssigneeId = editor })).Value!;

        var result = await svc.RescheduleAsync(editor, task.Id, DateTime.UtcNow.AddDays(3));

        Assert.False(result.Succeeded);
        Assert.Equal("Only the project owner can change the deadline.", result.Error);
    }

    [Fact]
    public async Task Editor_CannotDeleteATaskNotAssignedToThem()
    {
        using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var projectId = await TestDb.AddProjectAsync(ctx, owner);
        var editor = await AddEditorAsync(ctx, projectId, "Editor");
        var svc = Create(ctx);
        var task = (await svc.CreateTaskAsync(owner, projectId, new CreateTaskDto { Title = "T", AssigneeId = owner })).Value!;

        var result = await svc.DeleteTaskAsync(editor, task.Id);

        Assert.False(result.Succeeded);
        Assert.Equal("You can only change tasks assigned to you.", result.Error);
    }
}
