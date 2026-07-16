using System.Text.Json;
using Moq;
using Taskpilot.API.Common;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Marketplace;
using Taskpilot.API.DTOs.Projects;
using Taskpilot.API.Models;
using Taskpilot.API.Services;
using Taskpilot.API.Services.Assistant;
using Xunit;
// Two CreateTaskDto types exist (Projects vs Marketplace); the task service uses the Projects one.
using CreateTaskDto = Taskpilot.API.DTOs.Projects.CreateTaskDto;

namespace Taskpilot.API.Tests;

/// <summary>
/// Unit tests for the assistant's write tools. The business services are mocked so no real
/// mutation happens; the tests check that the toolbox resolves names, guards access, and
/// delegates to the services with the right arguments.
/// </summary>
public class AssistantActionsToolboxTests
{
    private static AssistantActionsToolbox Make(TaskpilotDbContext ctx, Mock<ITaskService>? tasks = null, Mock<IMarketplaceService>? market = null)
        => new(ctx, (tasks ?? new Mock<ITaskService>()).Object, (market ?? new Mock<IMarketplaceService>()).Object);

    [Fact]
    public async Task CreateTask_ResolvesProjectByName_AndDelegatesToService()
    {
        await using var ctx = TestDb.CreateContext();
        var me = await TestDb.AddUserAsync(ctx, "Me");
        var project = await TestDb.AddProjectAsync(ctx, me, "Nebula");

        var tasks = new Mock<ITaskService>();
        tasks.Setup(t => t.CreateTaskAsync(me, project, It.IsAny<CreateTaskDto>()))
            .ReturnsAsync(Result<TaskDto>.Ok(new TaskDto { Title = "Wire up auth", Status = "Backlog", Priority = "High" }));

        var box = Make(ctx, tasks);
        var json = await box.ExecuteAsync(me, "create_task", "{\"project\":\"Nebula\",\"title\":\"Wire up auth\",\"priority\":\"High\"}");

        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.GetProperty("created").GetBoolean());
        Assert.Equal("Wire up auth", doc.RootElement.GetProperty("task").GetProperty("title").GetString());
        tasks.Verify(t => t.CreateTaskAsync(me, project, It.Is<CreateTaskDto>(d => d.Title == "Wire up auth" && d.Priority == "High")), Times.Once);
    }

    [Fact]
    public async Task CreateTask_UnknownProject_DoesNotTouchTheService()
    {
        await using var ctx = TestDb.CreateContext();
        var me = await TestDb.AddUserAsync(ctx, "Me");
        var tasks = new Mock<ITaskService>();

        var box = Make(ctx, tasks);
        var json = await box.ExecuteAsync(me, "create_task", "{\"project\":\"Ghost\",\"title\":\"X\"}");

        Assert.True(JsonDocument.Parse(json).RootElement.TryGetProperty("error", out _));
        tasks.Verify(t => t.CreateTaskAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CreateTaskDto>()), Times.Never);
    }

    [Fact]
    public async Task ApplyToMarketplaceTask_ResolvesOpenGig_AndDefaultsRateToBudget()
    {
        await using var ctx = TestDb.CreateContext();
        var me = await TestDb.AddUserAsync(ctx, "Me");
        var poster = await TestDb.AddUserAsync(ctx, "Poster");
        var gigId = Guid.NewGuid();
        ctx.MarketplaceTasks.Add(new MarketplaceTask
        {
            Id = gigId, Title = "Build a landing page", Description = "d", Budget = 500m,
            Status = MarketplaceTaskStatus.Open, PosterId = poster,
        });
        await ctx.SaveChangesAsync();

        var market = new Mock<IMarketplaceService>();
        market.Setup(m => m.ApplyAsync(me, It.IsAny<ApplyDto>()))
            .ReturnsAsync(Result<ApplicationDto>.Ok(new ApplicationDto { TaskId = gigId, ProposedRate = 500m }));

        var box = Make(ctx, market: market);
        var json = await box.ExecuteAsync(me, "apply_to_marketplace_task", "{\"task\":\"landing page\"}");

        Assert.True(JsonDocument.Parse(json).RootElement.GetProperty("applied").GetBoolean());
        market.Verify(m => m.ApplyAsync(me, It.Is<ApplyDto>(d => d.TaskId == gigId && d.ProposedRate == 500m)), Times.Once);
    }

    [Fact]
    public async Task ApplyToMarketplaceTask_NoOpenGig_ReturnsErrorWithoutApplying()
    {
        await using var ctx = TestDb.CreateContext();
        var me = await TestDb.AddUserAsync(ctx, "Me");
        var market = new Mock<IMarketplaceService>();

        var box = Make(ctx, market: market);
        var json = await box.ExecuteAsync(me, "apply_to_marketplace_task", "{\"task\":\"nonexistent\"}");

        Assert.True(JsonDocument.Parse(json).RootElement.TryGetProperty("error", out _));
        market.Verify(m => m.ApplyAsync(It.IsAny<Guid>(), It.IsAny<ApplyDto>()), Times.Never);
    }
}

/// <summary>Tests that the composite toolbox merges definitions and routes calls correctly.</summary>
public class CompositeAssistantToolboxTests
{
    [Fact]
    public async Task MergesDefinitionsAndRoutesToTheOwningToolbox()
    {
        await using var ctx = TestDb.CreateContext();
        var me = await TestDb.AddUserAsync(ctx, "Me");
        await TestDb.AddProjectAsync(ctx, me, "Alpha");

        var read = new AssistantToolbox(ctx);
        var actions = new AssistantActionsToolbox(ctx, new Mock<ITaskService>().Object, new Mock<IMarketplaceService>().Object);
        var people = new AssistantPeopleToolbox(ctx, new Mock<IUserService>().Object);
        var composite = new CompositeAssistantToolbox(read, actions, people);

        var names = composite.Definitions.Select(d => d.Name).ToList();
        Assert.Contains("list_my_projects", names); // read tool
        Assert.Contains("create_task", names);      // write tool
        Assert.Contains("get_user_profile", names); // people tool

        // A read tool routes to the read toolbox and returns real data.
        var projects = await composite.ExecuteAsync(me, "list_my_projects", "{}");
        Assert.Equal(1, JsonDocument.Parse(projects).RootElement.GetProperty("count").GetInt32());

        // An unknown tool is reported as an error.
        var unknown = await composite.ExecuteAsync(me, "does_not_exist", "{}");
        Assert.True(JsonDocument.Parse(unknown).RootElement.TryGetProperty("error", out _));
    }
}
