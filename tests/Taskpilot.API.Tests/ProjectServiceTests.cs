using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Taskpilot.API.DTOs.Projects;
using Taskpilot.API.Models;
using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>Unit tests for <see cref="ProjectService"/> over an in-memory database.</summary>
public class ProjectServiceTests
{
    private static ProjectService Create(Taskpilot.API.Data.TaskpilotDbContext ctx) =>
        new(ctx, new Mock<IWebhookService>().Object, new Mock<INotificationService>().Object, NullLogger<ProjectService>.Instance);

    [Fact]
    public async Task CreateProject_StoresProjectForOwner()
    {
        using var ctx = TestDb.CreateContext();
        var ownerId = await TestDb.AddUserAsync(ctx, "Alice");
        var svc = Create(ctx);

        var result = await svc.CreateProjectAsync(ownerId, new SaveProjectDto { Name = "My Project", Color = "#fff" });

        Assert.True(result.Succeeded);
        Assert.Equal("My Project", result.Value!.Name);
        Assert.Equal("Alice", result.Value.OwnerName);
        Assert.False(result.Value.IsArchived);
        Assert.Equal(1, await ctx.Projects.CountAsync());
    }

    [Fact]
    public async Task GetProjects_ReturnsOnlyOwnersAndHidesArchivedByDefault()
    {
        using var ctx = TestDb.CreateContext();
        var alice = await TestDb.AddUserAsync(ctx, "Alice");
        var bob = await TestDb.AddUserAsync(ctx, "Bob");
        var svc = Create(ctx);

        await svc.CreateProjectAsync(alice, new SaveProjectDto { Name = "A1" });
        var a2 = await svc.CreateProjectAsync(alice, new SaveProjectDto { Name = "A2" });
        await svc.CreateProjectAsync(bob, new SaveProjectDto { Name = "B1" });
        await svc.SetArchivedAsync(alice, a2.Value!.Id, archived: true);

        var active = await svc.GetProjectsAsync(alice, includeArchived: false);
        var all = await svc.GetProjectsAsync(alice, includeArchived: true);

        Assert.Single(active.Value!);                 // only A1 (A2 archived, B1 is Bob's)
        Assert.Equal(2, all.Value!.Count);            // A1 + A2
    }

    [Fact]
    public async Task GetProject_OtherUser_NotFound()
    {
        using var ctx = TestDb.CreateContext();
        var alice = await TestDb.AddUserAsync(ctx, "Alice");
        var bob = await TestDb.AddUserAsync(ctx, "Bob");
        var svc = Create(ctx);
        var p = await svc.CreateProjectAsync(alice, new SaveProjectDto { Name = "Secret" });

        var result = await svc.GetProjectAsync(p.Value!.Id, bob);

        Assert.False(result.Succeeded);
        Assert.Equal("Project not found.", result.Error);
    }

    [Fact]
    public async Task ArchiveThenRestore_TogglesArchivedState()
    {
        using var ctx = TestDb.CreateContext();
        var alice = await TestDb.AddUserAsync(ctx, "Alice");
        var svc = Create(ctx);
        var p = await svc.CreateProjectAsync(alice, new SaveProjectDto { Name = "P" });

        await svc.SetArchivedAsync(alice, p.Value!.Id, archived: true);
        var archived = await svc.GetProjectAsync(p.Value.Id, alice);
        await svc.SetArchivedAsync(alice, p.Value.Id, archived: false);
        var restored = await svc.GetProjectAsync(p.Value.Id, alice);

        Assert.True(archived.Value!.IsArchived);
        Assert.False(restored.Value!.IsArchived);
    }

    private static async Task<Guid> AddTaskAsync(Taskpilot.API.Data.TaskpilotDbContext ctx, Guid projectId, Guid owner, string title)
    {
        var id = Guid.NewGuid();
        ctx.ProjectTasks.Add(new ProjectTask { Id = id, ProjectId = projectId, CreatorId = owner, Title = title, Status = ProjectTaskStatus.Backlog });
        await ctx.SaveChangesAsync();
        return id;
    }

    [Fact]
    public async Task CreateShareLink_GeneratesToken_AndPublicBoardResolves()
    {
        using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var svc = Create(ctx);
        var project = (await svc.CreateProjectAsync(owner, new SaveProjectDto { Name = "Roadmap", Color = "#123" })).Value!;
        await AddTaskAsync(ctx, project.Id, owner, "Public task");

        var link = await svc.CreateShareLinkAsync(owner, project.Id);
        Assert.True(link.Succeeded);
        Assert.True(link.Value!.Enabled);
        Assert.False(string.IsNullOrWhiteSpace(link.Value.Token));

        var board = await svc.GetPublicBoardAsync(link.Value.Token!);
        Assert.True(board.Succeeded);
        Assert.Equal("Roadmap", board.Value!.Name);
        Assert.Contains(board.Value.Tasks, t => t.Title == "Public task");
    }

    [Fact]
    public async Task CreateShareLink_IsIdempotent()
    {
        using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var svc = Create(ctx);
        var project = (await svc.CreateProjectAsync(owner, new SaveProjectDto { Name = "P" })).Value!;

        var first = (await svc.CreateShareLinkAsync(owner, project.Id)).Value!.Token;
        var second = (await svc.CreateShareLinkAsync(owner, project.Id)).Value!.Token;
        Assert.Equal(first, second);
    }

    [Fact]
    public async Task ShareLink_ByNonOwner_Fails()
    {
        using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var stranger = await TestDb.AddUserAsync(ctx, "Stranger");
        var svc = Create(ctx);
        var project = (await svc.CreateProjectAsync(owner, new SaveProjectDto { Name = "P" })).Value!;

        Assert.False((await svc.CreateShareLinkAsync(stranger, project.Id)).Succeeded);
    }

    [Fact]
    public async Task RevokeShareLink_MakesTheBoardUnavailable()
    {
        using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var svc = Create(ctx);
        var project = (await svc.CreateProjectAsync(owner, new SaveProjectDto { Name = "P" })).Value!;
        var token = (await svc.CreateShareLinkAsync(owner, project.Id)).Value!.Token!;

        Assert.True((await svc.RevokeShareLinkAsync(owner, project.Id)).Succeeded);
        Assert.False((await svc.GetPublicBoardAsync(token)).Succeeded);
    }

    [Fact]
    public async Task GetPublicBoard_UnknownToken_Fails()
    {
        using var ctx = TestDb.CreateContext();
        Assert.False((await Create(ctx).GetPublicBoardAsync("nope")).Succeeded);
    }
}
