using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Taskpilot.API.DTOs.Projects;
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
}
