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
    public async Task GetOverdueTasks_CoversMyProjectsAndNamesTheAssignee()
    {
        await using var ctx = TestDb.CreateContext();
        var me = await TestDb.AddUserAsync(ctx, "Me");
        var teammate = await TestDb.AddUserAsync(ctx, "Teammate");
        var stranger = await TestDb.AddUserAsync(ctx, "Stranger");
        var mine = await TestDb.AddProjectAsync(ctx, me, "Mine");
        var foreign = await TestDb.AddProjectAsync(ctx, stranger, "Foreign"); // I have no access

        await AddTaskAsync(ctx, mine, me, ProjectTaskStatus.InProgress, DateTime.UtcNow.AddDays(-2), "Mine overdue");
        // A teammate's overdue task in my project must still appear — this is the dashboard's behaviour.
        await AddTaskAsync(ctx, mine, teammate, ProjectTaskStatus.InProgress, DateTime.UtcNow.AddDays(-3), "Teammate overdue");
        await AddTaskAsync(ctx, mine, me, ProjectTaskStatus.Done, DateTime.UtcNow.AddDays(-2), "Mine done");       // excluded (done)
        await AddTaskAsync(ctx, mine, me, ProjectTaskStatus.InProgress, DateTime.UtcNow.AddDays(2), "Mine future"); // excluded (not overdue)
        await AddTaskAsync(ctx, foreign, stranger, ProjectTaskStatus.InProgress, DateTime.UtcNow.AddDays(-2), "Hidden"); // excluded (no access)

        var toolbox = new AssistantToolbox(ctx);
        var json = await toolbox.ExecuteAsync(me, "get_overdue_tasks", "{}");

        using var doc = JsonDocument.Parse(json);
        Assert.Equal(2, doc.RootElement.GetProperty("count").GetInt32());
        var tasks = doc.RootElement.GetProperty("tasks");
        var titles = tasks.EnumerateArray().Select(x => x.GetProperty("title").GetString()).ToList();
        Assert.Contains("Mine overdue", titles);
        Assert.Contains("Teammate overdue", titles);
        Assert.DoesNotContain("Hidden", titles);
        // The assignee is reported so the assistant can answer "who is it assigned to?".
        Assert.Equal("Teammate", tasks[0].GetProperty("assignee").GetString()); // ordered by deadline; -3d first
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

    [Theory]
    [InlineData("null")]   // model omitted arguments (JSON null)
    [InlineData("")]        // empty arguments
    [InlineData("[]")]      // non-object arguments
    public async Task ToolsTolerateMissingOrNonObjectArguments(string args)
    {
        await using var ctx = TestDb.CreateContext();
        var me = await TestDb.AddUserAsync(ctx, "Me");
        var project = await TestDb.AddProjectAsync(ctx, me);
        await AddTaskAsync(ctx, project, me, ProjectTaskStatus.InProgress, DateTime.UtcNow.AddDays(3), "Soon");

        var toolbox = new AssistantToolbox(ctx);

        // Must not throw — a bare tool call with no arguments should still work with defaults.
        var upcoming = await toolbox.ExecuteAsync(me, "get_upcoming_deadlines", args);
        Assert.Equal(1, JsonDocument.Parse(upcoming).RootElement.GetProperty("count").GetInt32());

        var mine = await toolbox.ExecuteAsync(me, "get_my_tasks", args);
        Assert.Equal(1, JsonDocument.Parse(mine).RootElement.GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task GetUpcomingDeadlines_AcceptsDaysAsString()
    {
        await using var ctx = TestDb.CreateContext();
        var me = await TestDb.AddUserAsync(ctx, "Me");
        var project = await TestDb.AddProjectAsync(ctx, me);
        await AddTaskAsync(ctx, project, me, ProjectTaskStatus.InProgress, DateTime.UtcNow.AddDays(3), "Soon");
        await AddTaskAsync(ctx, project, me, ProjectTaskStatus.InProgress, DateTime.UtcNow.AddDays(10), "Later");

        var toolbox = new AssistantToolbox(ctx);
        // Some models pass numbers as strings.
        var json = await toolbox.ExecuteAsync(me, "get_upcoming_deadlines", "{\"days\":\"5\"}");

        using var doc = JsonDocument.Parse(json);
        Assert.Equal(5, doc.RootElement.GetProperty("days").GetInt32());
        Assert.Equal(1, doc.RootElement.GetProperty("count").GetInt32()); // only the 3-day task
    }

    [Fact]
    public async Task ListMarketplaceTasks_ReturnsGigsWithDetailsAndFiltersByStatus()
    {
        await using var ctx = TestDb.CreateContext();
        var poster = await TestDb.AddUserAsync(ctx, "Poster");
        ctx.MarketplaceTasks.Add(new MarketplaceTask
        {
            Id = Guid.NewGuid(), Title = "Need a logo", Description = "d", Budget = 200m,
            RequiredSkills = "Design", Status = MarketplaceTaskStatus.Open, PosterId = poster,
        });
        ctx.MarketplaceTasks.Add(new MarketplaceTask
        {
            Id = Guid.NewGuid(), Title = "Build X", Description = "d", Budget = 500m,
            Status = MarketplaceTaskStatus.Completed, PosterId = poster,
        });
        await ctx.SaveChangesAsync();

        // Any user can see the marketplace, so scoping is not required — pass an unrelated id.
        var toolbox = new AssistantToolbox(ctx);

        var all = await toolbox.ExecuteAsync(Guid.NewGuid(), "list_marketplace_tasks", "{}");
        Assert.Equal(2, JsonDocument.Parse(all).RootElement.GetProperty("count").GetInt32());

        var open = await toolbox.ExecuteAsync(Guid.NewGuid(), "list_marketplace_tasks", "{\"status\":\"Open\"}");
        using var doc = JsonDocument.Parse(open);
        Assert.Equal(1, doc.RootElement.GetProperty("count").GetInt32());
        var gig = doc.RootElement.GetProperty("tasks")[0];
        Assert.Equal("Need a logo", gig.GetProperty("title").GetString());
        Assert.Equal(200, gig.GetProperty("budget").GetInt32());
        Assert.Equal("Poster", gig.GetProperty("poster").GetString());
    }

    [Fact]
    public async Task Search_FindsMatchesButRespectsProjectAccess()
    {
        await using var ctx = TestDb.CreateContext();
        var me = await TestDb.AddUserAsync(ctx, "Me");
        var stranger = await TestDb.AddUserAsync(ctx, "Designer Dan");
        var mine = await TestDb.AddProjectAsync(ctx, me, "Alpha");
        var foreign = await TestDb.AddProjectAsync(ctx, stranger, "Beta");
        await AddTaskAsync(ctx, mine, me, ProjectTaskStatus.InProgress, null, "Design homepage");
        await AddTaskAsync(ctx, foreign, stranger, ProjectTaskStatus.InProgress, null, "Design logo"); // no access
        ctx.ForumTopics.Add(new ForumTopic { Id = Guid.NewGuid(), Title = "Design tips", Body = "b", AuthorId = stranger });
        await ctx.SaveChangesAsync();

        var toolbox = new AssistantToolbox(ctx);
        var json = await toolbox.ExecuteAsync(me, "search_taskpilot", "{\"query\":\"design\"}");

        using var doc = JsonDocument.Parse(json);
        var taskTitles = doc.RootElement.GetProperty("tasks").EnumerateArray().Select(x => x.GetProperty("title").GetString()).ToList();
        Assert.Contains("Design homepage", taskTitles);
        Assert.DoesNotContain("Design logo", taskTitles); // task in a project I can't access
        Assert.Single(doc.RootElement.GetProperty("topics").EnumerateArray());
        Assert.Single(doc.RootElement.GetProperty("users").EnumerateArray()); // "Designer Dan"
    }

    [Fact]
    public async Task GetNotifications_ReturnsUnreadOnly()
    {
        await using var ctx = TestDb.CreateContext();
        var me = await TestDb.AddUserAsync(ctx, "Me");
        ctx.Notifications.Add(new Notification { Id = Guid.NewGuid(), RecipientId = me, Type = NotificationType.Task, Message = "New task assigned", IsRead = false });
        ctx.Notifications.Add(new Notification { Id = Guid.NewGuid(), RecipientId = me, Type = NotificationType.General, Message = "Old news", IsRead = true });
        await ctx.SaveChangesAsync();

        var toolbox = new AssistantToolbox(ctx);
        var json = await toolbox.ExecuteAsync(me, "get_notifications", "{}");

        using var doc = JsonDocument.Parse(json);
        Assert.Equal(1, doc.RootElement.GetProperty("unread").GetInt32());
        Assert.Equal("New task assigned", doc.RootElement.GetProperty("notifications")[0].GetProperty("message").GetString());
    }

    [Fact]
    public async Task GetProjectStats_BreaksDownByStatusAndWorkload()
    {
        await using var ctx = TestDb.CreateContext();
        var me = await TestDb.AddUserAsync(ctx, "Me");
        var mate = await TestDb.AddUserAsync(ctx, "Mate");
        var project = await TestDb.AddProjectAsync(ctx, me, "Progress");
        await AddTaskAsync(ctx, project, me, ProjectTaskStatus.Done, null, "d1");
        await AddTaskAsync(ctx, project, me, ProjectTaskStatus.Done, null, "d2");
        await AddTaskAsync(ctx, project, me, ProjectTaskStatus.InProgress, null, "wip");
        await AddTaskAsync(ctx, project, mate, ProjectTaskStatus.InProgress, DateTime.UtcNow.AddDays(-1), "late"); // overdue

        var toolbox = new AssistantToolbox(ctx);
        var json = await toolbox.ExecuteAsync(me, "get_project_stats", "{\"project\":\"Progress\"}");

        using var doc = JsonDocument.Parse(json);
        Assert.Equal("Progress", doc.RootElement.GetProperty("project").GetString());
        Assert.Equal(4, doc.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(1, doc.RootElement.GetProperty("overdue").GetInt32());
        Assert.Equal(2, doc.RootElement.GetProperty("byStatus").GetProperty("Done").GetInt32());
    }

    [Fact]
    public async Task GetProjectStats_UnknownProject_ReturnsError()
    {
        await using var ctx = TestDb.CreateContext();
        var me = await TestDb.AddUserAsync(ctx, "Me");
        var toolbox = new AssistantToolbox(ctx);

        var json = await toolbox.ExecuteAsync(me, "get_project_stats", "{\"project\":\"Nope\"}");
        Assert.True(JsonDocument.Parse(json).RootElement.TryGetProperty("error", out _));
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
