using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Taskpilot.API.Data;
using Taskpilot.API.Models;
using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>
/// Tests for <see cref="ProjectTemplateService"/> — saving a project as a template, listing
/// and previewing templates, and stamping fresh projects out of them. A real
/// <see cref="ProjectService"/> runs alongside so the create path returns a genuine ProjectDto.
/// </summary>
public class ProjectTemplateServiceTests
{
    private ProjectTemplateService Create(TaskpilotDbContext ctx)
    {
        var projects = new ProjectService(ctx, new Mock<IWebhookService>().Object,
            new Mock<INotificationService>().Object, NullLogger<ProjectService>.Instance);
        return new ProjectTemplateService(ctx, projects, new Mock<IWebhookService>().Object,
            NullLogger<ProjectTemplateService>.Instance);
    }

    /// <summary>Adds a task to a project and returns its id.</summary>
    private static async Task<Guid> AddTaskAsync(
        TaskpilotDbContext ctx, Guid projectId, Guid creatorId, string title,
        DateTime? deadline = null, Guid? parentId = null, params string[] tags)
    {
        var id = Guid.NewGuid();
        ctx.ProjectTasks.Add(new ProjectTask
        {
            Id = id,
            ProjectId = projectId,
            Title = title,
            Status = ProjectTaskStatus.InProgress,   // set, to prove it is NOT copied
            Priority = TaskPriority.High,
            CreatorId = creatorId,
            AssigneeId = creatorId,                   // set, to prove it is NOT copied
            ParentTaskId = parentId,
            Deadline = deadline,
            Tags = tags.ToList(),
        });
        await ctx.SaveChangesAsync();
        return id;
    }

    [Fact]
    public async Task SaveAsTemplate_SnapshotsTasks_RelativeDeadline_AndSubtaskLinks()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var projectId = Guid.NewGuid();
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        ctx.Projects.Add(new Project { Id = projectId, Name = "Launch", Description = "d", Color = "#111", OwnerId = owner, CreatedAt = start });
        await ctx.SaveChangesAsync();
        var parent = await AddTaskAsync(ctx, projectId, owner, "Parent", start.AddDays(5), null, "a", "b");
        await AddTaskAsync(ctx, projectId, owner, "Child", null, parent);
        var svc = Create(ctx);

        var result = await svc.SaveAsTemplateAsync(owner, projectId, name: null);

        Assert.True(result.Succeeded);
        Assert.Equal("Launch", result.Value!.Name);   // defaults to the project name
        Assert.Equal(2, result.Value.TaskCount);

        var detail = (await svc.GetTemplateAsync(owner, result.Value.Id)).Value!;
        var parentTpl = detail.Tasks.Single(t => t.Title == "Parent");
        var childTpl = detail.Tasks.Single(t => t.Title == "Child");
        Assert.Equal(5, parentTpl.DeadlineOffsetDays);          // 5 days from project start
        Assert.Equal(new[] { "a", "b" }, parentTpl.Tags.ToArray());
        Assert.Null(childTpl.DeadlineOffsetDays);               // no deadline -> null offset
        Assert.Equal(parentTpl.Id, childTpl.ParentTemplateTaskId); // subtask link survived
    }

    [Fact]
    public async Task SaveAsTemplate_UsesTheGivenName_WhenProvided()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var projectId = await TestDb.AddProjectAsync(ctx, owner, "Original");
        var svc = Create(ctx);

        var result = await svc.SaveAsTemplateAsync(owner, projectId, name: "  My blueprint  ");

        Assert.Equal("My blueprint", result.Value!.Name);
    }

    [Fact]
    public async Task SaveAsTemplate_ByAMember_IsAllowed()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var member = await TestDb.AddUserAsync(ctx, "Member");
        var projectId = await TestDb.AddProjectAsync(ctx, owner);
        ctx.ProjectMembers.Add(new ProjectMember
        {
            Id = Guid.NewGuid(), ProjectId = projectId, UserId = member, Role = ProjectMemberRole.Viewer,
        });
        await ctx.SaveChangesAsync();
        var svc = Create(ctx);

        // A member can snapshot a project they can see; the template becomes theirs.
        var result = await svc.SaveAsTemplateAsync(member, projectId, name: null);

        Assert.True(result.Succeeded);
        Assert.Equal(member, await ctx.ProjectTemplates.Where(t => t.Id == result.Value!.Id).Select(t => t.OwnerId).FirstAsync());
    }

    [Fact]
    public async Task SaveAsTemplate_ByAnOutsider_IsRefused()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var outsider = await TestDb.AddUserAsync(ctx, "Outsider");
        var projectId = await TestDb.AddProjectAsync(ctx, owner);
        var svc = Create(ctx);

        var result = await svc.SaveAsTemplateAsync(outsider, projectId, name: null);

        Assert.False(result.Succeeded);
        Assert.Equal("Project not found.", result.Error);
        Assert.Equal(0, await ctx.ProjectTemplates.CountAsync());
    }

    [Fact]
    public async Task CreateProjectFromTemplate_StampsTasks_Fresh_WithAbsoluteDeadlines()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var projectId = Guid.NewGuid();
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        ctx.Projects.Add(new Project { Id = projectId, Name = "Src", OwnerId = owner, CreatedAt = start });
        await ctx.SaveChangesAsync();
        var parent = await AddTaskAsync(ctx, projectId, owner, "Parent", start.AddDays(3));
        await AddTaskAsync(ctx, projectId, owner, "Child", null, parent);
        var svc = Create(ctx);
        var template = (await svc.SaveAsTemplateAsync(owner, projectId, "Tpl")).Value!;

        var result = await svc.CreateProjectFromTemplateAsync(owner, template.Id, name: "Q2 launch", color: "#abc");

        Assert.True(result.Succeeded);
        Assert.Equal("Q2 launch", result.Value!.Name);
        Assert.Equal(2, result.Value.TaskCount);
        var tasks = await ctx.ProjectTasks.Where(t => t.ProjectId == result.Value.Id).ToListAsync();
        // Every instantiated task starts clean: Backlog, unassigned, creator = the user.
        Assert.All(tasks, t => Assert.Equal(ProjectTaskStatus.Backlog, t.Status));
        Assert.All(tasks, t => Assert.Null(t.AssigneeId));
        Assert.All(tasks, t => Assert.Equal(owner, t.CreatorId));
        // The relative offset became an absolute deadline ~3 days from now.
        var parentTask = tasks.Single(t => t.Title == "Parent");
        Assert.NotNull(parentTask.Deadline);
        Assert.InRange((parentTask.Deadline!.Value - DateTime.UtcNow).TotalDays, 2.9, 3.1);
        // Subtask link survived the round trip.
        var childTask = tasks.Single(t => t.Title == "Child");
        Assert.Equal(parentTask.Id, childTask.ParentTaskId);
        Assert.Null(childTask.Deadline);
    }

    [Fact]
    public async Task CreateProjectFromTemplate_OfSomeoneElsesTemplate_IsRefused()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var other = await TestDb.AddUserAsync(ctx, "Other");
        var projectId = await TestDb.AddProjectAsync(ctx, owner);
        var svc = Create(ctx);
        var template = (await svc.SaveAsTemplateAsync(owner, projectId, "Tpl")).Value!;

        var result = await svc.CreateProjectFromTemplateAsync(other, template.Id, null, null);

        Assert.False(result.Succeeded);
        Assert.Equal("Template not found.", result.Error);
    }

    [Fact]
    public async Task GetTemplates_ReturnsOnlyTheUsersOwn()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var other = await TestDb.AddUserAsync(ctx, "Other");
        var p1 = await TestDb.AddProjectAsync(ctx, owner);
        var p2 = await TestDb.AddProjectAsync(ctx, other);
        var svc = Create(ctx);
        await svc.SaveAsTemplateAsync(owner, p1, "Mine");
        await svc.SaveAsTemplateAsync(other, p2, "Theirs");

        var mine = (await svc.GetTemplatesAsync(owner)).Value!;

        Assert.Single(mine);
        Assert.Equal("Mine", mine[0].Name);
    }

    [Fact]
    public async Task DeleteTemplate_RemovesItAndItsTasks()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var projectId = Guid.NewGuid();
        ctx.Projects.Add(new Project { Id = projectId, Name = "P", OwnerId = owner, CreatedAt = DateTime.UtcNow });
        await ctx.SaveChangesAsync();
        await AddTaskAsync(ctx, projectId, owner, "T1");
        var svc = Create(ctx);
        var template = (await svc.SaveAsTemplateAsync(owner, projectId, "Tpl")).Value!;
        Assert.Equal(1, await ctx.ProjectTemplateTasks.CountAsync());

        var result = await svc.DeleteTemplateAsync(owner, template.Id);

        Assert.True(result.Succeeded);
        Assert.Equal(0, await ctx.ProjectTemplates.CountAsync());
        Assert.Equal(0, await ctx.ProjectTemplateTasks.CountAsync());
    }

    [Fact]
    public async Task DeleteTemplate_OfSomeoneElse_IsRefused()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var other = await TestDb.AddUserAsync(ctx, "Other");
        var projectId = await TestDb.AddProjectAsync(ctx, owner);
        var svc = Create(ctx);
        var template = (await svc.SaveAsTemplateAsync(owner, projectId, "Tpl")).Value!;

        var result = await svc.DeleteTemplateAsync(other, template.Id);

        Assert.False(result.Succeeded);
        Assert.Equal(1, await ctx.ProjectTemplates.CountAsync());
    }
}
