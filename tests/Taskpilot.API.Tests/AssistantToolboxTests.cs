using System.Text.Json;
using Taskpilot.API.Data;
using Taskpilot.API.Models;
using Taskpilot.API.Services.Assistant;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>
/// Unit tests for <see cref="AssistantToolbox"/> — the read-only tools the assistant runs.
/// Every query must be scoped to the calling user.
/// </summary>
public class AssistantToolboxTests
{
    private static async Task<Guid> AddTaskAsync(
        TaskpilotDbContext ctx, Guid projectId, Guid? assigneeId,
        ProjectTaskStatus status, DateTime? deadline, string title = "T")
    {
        var id = Guid.NewGuid();
        ctx.ProjectTasks.Add(new ProjectTask
        {
            Id = id, ProjectId = projectId, Title = title, Status = status,
            AssigneeId = assigneeId, CreatorId = assigneeId ?? Guid.NewGuid(), Deadline = deadline,
        });
        await ctx.SaveChangesAsync();
        return id;
    }

    [Fact]
    public async Task GetOverdueTasks_OnlyMineAndOverdue()
    {
        await using var ctx = TestDb.CreateContext();
        var me = await TestDb.AddUserAsync(ctx, "Me");
        var other = await TestDb.AddUserAsync(ctx, "Other");
        var project = await TestDb.AddProjectAsync(ctx, me);
        await AddTaskAsync(ctx, project, me, ProjectTaskStatus.InProgress, DateTime.UtcNow.AddDays(-2), "Mine overdue");
        await AddTaskAsync(ctx, project, me, ProjectTaskStatus.Done, DateTime.UtcNow.AddDays(-2), "Mine done");        // excluded (done)
        await AddTaskAsync(ctx, project, me, ProjectTaskStatus.InProgress, DateTime.UtcNow.AddDays(2), "Mine future");  // excluded (not overdue)
        await AddTaskAsync(ctx, project, other, ProjectTaskStatus.InProgress, DateTime.UtcNow.AddDays(-2), "Not mine"); // excluded (other user)

        var toolbox = new AssistantToolbox(ctx);
        var json = await toolbox.ExecuteAsync(me, "get_overdue_tasks", "{}");

        using var doc = JsonDocument.Parse(json);
        Assert.Equal(1, doc.RootElement.GetProperty("count").GetInt32());
        Assert.Equal("Mine overdue", doc.RootElement.GetProperty("tasks")[0].GetProperty("title").GetString());
    }

    [Fact]
    public async Task GetUpcomingDeadlines_WithinTheWindow()
    {
        await using var ctx = TestDb.CreateContext();
        var me = await TestDb.AddUserAsync(ctx, "Me");
        var project = await TestDb.AddProjectAsync(ctx, me);
        await AddTaskAsync(ctx, project, me, ProjectTaskStatus.InProgress, DateTime.UtcNow.AddDays(3), "Soon");
        await AddTaskAsync(ctx, project, me, ProjectTaskStatus.InProgress, DateTime.UtcNow.AddDays(20), "Later"); // outside 7-day window

        var toolbox = new AssistantToolbox(ctx);
        var json = await toolbox.ExecuteAsync(me, "get_upcoming_deadlines", "{\"days\":7}");

        using var doc = JsonDocument.Parse(json);
        Assert.Equal(1, doc.RootElement.GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task GetMyTasks_FiltersByStatus()
    {
        await using var ctx = TestDb.CreateContext();
        var me = await TestDb.AddUserAsync(ctx, "Me");
        var project = await TestDb.AddProjectAsync(ctx, me);
        await AddTaskAsync(ctx, project, me, ProjectTaskStatus.InProgress, null, "A");
        await AddTaskAsync(ctx, project, me, ProjectTaskStatus.Done, null, "B");

        var toolbox = new AssistantToolbox(ctx);
        var json = await toolbox.ExecuteAsync(me, "get_my_tasks", "{\"status\":\"Done\"}");

        using var doc = JsonDocument.Parse(json);
        Assert.Equal(1, doc.RootElement.GetProperty("count").GetInt32());
        Assert.Equal("B", doc.RootElement.GetProperty("tasks")[0].GetProperty("title").GetString());
    }

    [Fact]
    public async Task ListMyProjects_OwnedOrMember_WithCounts()
    {
        await using var ctx = TestDb.CreateContext();
        var me = await TestDb.AddUserAsync(ctx, "Me");
        var other = await TestDb.AddUserAsync(ctx, "Other");
        var mine = await TestDb.AddProjectAsync(ctx, me, "Mine");
        await TestDb.AddProjectAsync(ctx, other, "Theirs"); // not mine → excluded
        await AddTaskAsync(ctx, mine, me, ProjectTaskStatus.Done, null);

        var toolbox = new AssistantToolbox(ctx);
        var json = await toolbox.ExecuteAsync(me, "list_my_projects", "{}");

        using var doc = JsonDocument.Parse(json);
        Assert.Equal(1, doc.RootElement.GetProperty("count").GetInt32());
        var proj = doc.RootElement.GetProperty("projects")[0];
        Assert.Equal("Mine", proj.GetProperty("name").GetString());
        Assert.Equal(1, proj.GetProperty("tasks").GetInt32());
        Assert.Equal(1, proj.GetProperty("done").GetInt32());
    }

    [Fact]
    public async Task UnknownTool_ReturnsError()
    {
        await using var ctx = TestDb.CreateContext();
        var me = await TestDb.AddUserAsync(ctx, "Me");
        var toolbox = new AssistantToolbox(ctx);

        var json = await toolbox.ExecuteAsync(me, "delete_everything", "{}");

        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("error", out _));
    }
}
