using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Taskpilot.API.Data;
using Taskpilot.API.Models;
using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>
/// Tests muting a project (spec module 6): the flag lives on the member row, is surfaced
/// per-user on the project list and detail, and silencing itself is covered where the
/// notifications are sent (see <see cref="NotificationServiceTests"/>).
/// </summary>
public class ProjectMuteTests
{
    private static ProjectService Create(TaskpilotDbContext ctx) =>
        new(ctx, Mock.Of<IWebhookService>(), Mock.Of<INotificationService>(), NullLogger<ProjectService>.Instance);

    /// <summary>Adds the given user as a member of the project.</summary>
    private static async Task AddMemberAsync(TaskpilotDbContext ctx, Guid projectId, Guid userId)
    {
        ctx.ProjectMembers.Add(new ProjectMember { Id = Guid.NewGuid(), ProjectId = projectId, UserId = userId });
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task SetMuted_SetsTheFlag_AndReflectsPerMemberInListAndDetail()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var member = await TestDb.AddUserAsync(ctx, "Member");
        var projectId = await TestDb.AddProjectAsync(ctx, owner, "Alpha");
        await AddMemberAsync(ctx, projectId, member);
        var svc = Create(ctx);

        var result = await svc.SetProjectMutedAsync(member, projectId, muted: true);

        Assert.True(result.Succeeded);
        Assert.True(result.Value);
        // The member sees it muted in both the list and the detail view...
        var list = (await svc.GetProjectsAsync(member, includeArchived: false)).Value!;
        Assert.True(list.Single(p => p.Id == projectId).Muted);
        var detail = (await svc.GetProjectAsync(projectId, member)).Value!;
        Assert.True(detail.Muted);
        // ...but the owner (a different user) does not — mute is per-member.
        var ownerList = (await svc.GetProjectsAsync(owner, includeArchived: false)).Value!;
        Assert.False(ownerList.Single(p => p.Id == projectId).Muted);
    }

    [Fact]
    public async Task SetMuted_ByANonMember_IsRefused()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var outsider = await TestDb.AddUserAsync(ctx, "Outsider");
        var projectId = await TestDb.AddProjectAsync(ctx, owner, "Alpha");
        var svc = Create(ctx);

        var result = await svc.SetProjectMutedAsync(outsider, projectId, muted: true);

        Assert.False(result.Succeeded);
        Assert.Equal("You are not a member of this project.", result.Error);
    }

    [Fact]
    public async Task Unmute_ClearsTheFlag()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var member = await TestDb.AddUserAsync(ctx, "Member");
        var projectId = await TestDb.AddProjectAsync(ctx, owner, "Alpha");
        await AddMemberAsync(ctx, projectId, member);
        var svc = Create(ctx);
        await svc.SetProjectMutedAsync(member, projectId, muted: true);

        var result = await svc.SetProjectMutedAsync(member, projectId, muted: false);

        Assert.True(result.Succeeded);
        Assert.False(result.Value);
        var detail = (await svc.GetProjectAsync(projectId, member)).Value!;
        Assert.False(detail.Muted);
    }
}
