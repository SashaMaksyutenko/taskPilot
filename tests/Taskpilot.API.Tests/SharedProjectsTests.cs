using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Taskpilot.API.Data;
using Taskpilot.API.Models;
using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>
/// Tests shared-project participation on a profile (feedback #8a): only projects BOTH the
/// profile user and the viewer take part in are listed, so private project names never leak.
/// Archived projects are excluded; your own profile lists all of your projects.
/// </summary>
public class SharedProjectsTests
{
    private static UserService Create(TaskpilotDbContext ctx) =>
        new(ctx, Mock.Of<IFileService>(), NullLogger<UserService>.Instance);

    private static void AddMember(TaskpilotDbContext ctx, Guid projectId, Guid userId) =>
        ctx.ProjectMembers.Add(new ProjectMember { Id = Guid.NewGuid(), ProjectId = projectId, UserId = userId });

    [Fact]
    public async Task SharedProjects_ListsOnlyProjectsBothTakePartIn()
    {
        await using var ctx = TestDb.CreateContext();
        var alice = await TestDb.AddUserAsync(ctx, "Alice");
        var bob = await TestDb.AddUserAsync(ctx, "Bob");
        // Shared: Alice owns "Together", Bob is a member.
        var shared = await TestDb.AddProjectAsync(ctx, alice, "Together");
        AddMember(ctx, shared, bob);
        // Alice-only project (Bob is not in it) — must NOT show to Bob.
        await TestDb.AddProjectAsync(ctx, alice, "Alice Secret");
        await ctx.SaveChangesAsync();
        var svc = Create(ctx);

        // Bob viewing Alice's profile sees only the shared project.
        var result = await svc.GetSharedProjectsAsync(profileUserId: alice, viewerId: bob);

        Assert.True(result.Succeeded);
        Assert.Equal(new[] { "Together" }, result.Value!.Select(p => p.Name).ToArray());
    }

    [Fact]
    public async Task SharedProjects_OnOwnProfile_ListsAllOwnActiveProjects()
    {
        await using var ctx = TestDb.CreateContext();
        var alice = await TestDb.AddUserAsync(ctx, "Alice");
        await TestDb.AddProjectAsync(ctx, alice, "P1");
        await TestDb.AddProjectAsync(ctx, alice, "P2");
        var archived = await TestDb.AddProjectAsync(ctx, alice, "Old");
        (await ctx.Projects.FindAsync(archived))!.ArchivedAt = DateTime.UtcNow;
        await ctx.SaveChangesAsync();
        var svc = Create(ctx);

        var result = await svc.GetSharedProjectsAsync(profileUserId: alice, viewerId: alice);

        // Both active projects, sorted by name; the archived one is excluded.
        Assert.Equal(new[] { "P1", "P2" }, result.Value!.Select(p => p.Name).ToArray());
    }

    [Fact]
    public async Task SharedProjects_NoOverlap_ReturnsEmpty()
    {
        await using var ctx = TestDb.CreateContext();
        var alice = await TestDb.AddUserAsync(ctx, "Alice");
        var stranger = await TestDb.AddUserAsync(ctx, "Stranger");
        await TestDb.AddProjectAsync(ctx, alice, "Alice Only");
        var svc = Create(ctx);

        var result = await svc.GetSharedProjectsAsync(profileUserId: alice, viewerId: stranger);

        Assert.Empty(result.Value!);
    }
}
