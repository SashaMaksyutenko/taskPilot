using Taskpilot.API.Data;
using Taskpilot.API.Models;
using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>
/// Tests for the collaborative-editing service: who may co-edit a document (write access to the
/// underlying task) and the snapshot round-trip. The CRDT relay itself lives in the hub and is
/// exercised in-browser, not here.
/// </summary>
public class CollabServiceTests
{
    private static async Task<(Guid ownerId, Guid editorId, Guid taskId)> SeedAsync(TaskpilotDbContext ctx)
    {
        var ownerId = await TestDb.AddUserAsync(ctx, "Owner");
        var editorId = await TestDb.AddUserAsync(ctx, "Editor");
        var projectId = await TestDb.AddProjectAsync(ctx, ownerId);
        ctx.ProjectMembers.Add(new ProjectMember
        {
            Id = Guid.NewGuid(), ProjectId = projectId, UserId = editorId, Role = ProjectMemberRole.Editor,
        });
        var taskId = Guid.NewGuid();
        ctx.ProjectTasks.Add(new ProjectTask
        {
            Id = taskId, ProjectId = projectId, Title = "Design the API", CreatorId = ownerId, AssigneeId = editorId,
        });
        await ctx.SaveChangesAsync();
        return (ownerId, editorId, taskId);
    }

    [Fact]
    public async Task CanAccess_AllowsOwnerAndAssignedEditor()
    {
        await using var ctx = TestDb.CreateContext();
        var (ownerId, editorId, taskId) = await SeedAsync(ctx);
        var svc = new CollabService(ctx);

        Assert.True(await svc.CanAccessAsync($"task:{taskId}", ownerId));
        Assert.True(await svc.CanAccessAsync($"task:{taskId}", editorId));
    }

    [Fact]
    public async Task CanAccess_DeniesEditorNotAssignedToTheTask()
    {
        await using var ctx = TestDb.CreateContext();
        var (ownerId, _, _) = await SeedAsync(ctx);
        // A second Editor member who is NOT the assignee cannot modify this task.
        var otherEditor = await TestDb.AddUserAsync(ctx, "Other");
        var projectId = ctx.ProjectTasks.Single().ProjectId;
        ctx.ProjectMembers.Add(new ProjectMember
        {
            Id = Guid.NewGuid(), ProjectId = projectId, UserId = otherEditor, Role = ProjectMemberRole.Editor,
        });
        var task2 = new ProjectTask { Id = Guid.NewGuid(), ProjectId = projectId, Title = "Other task", CreatorId = ownerId };
        ctx.ProjectTasks.Add(task2);
        await ctx.SaveChangesAsync();

        Assert.False(await new CollabService(ctx).CanAccessAsync($"task:{task2.Id}", otherEditor));
    }

    [Fact]
    public async Task CanAccess_DeniesNonMember()
    {
        await using var ctx = TestDb.CreateContext();
        var (_, _, taskId) = await SeedAsync(ctx);
        var stranger = await TestDb.AddUserAsync(ctx, "Stranger");

        Assert.False(await new CollabService(ctx).CanAccessAsync($"task:{taskId}", stranger));
    }

    [Theory]
    [InlineData("garbage")]
    [InlineData("note:123")]
    [InlineData("task:not-a-guid")]
    [InlineData("task:")]
    public async Task CanAccess_DeniesMalformedDocId(string docId)
    {
        await using var ctx = TestDb.CreateContext();
        var (ownerId, _, _) = await SeedAsync(ctx);

        Assert.False(await new CollabService(ctx).CanAccessAsync(docId, ownerId));
    }

    [Fact]
    public async Task SaveState_UpsertsAndGetStateReadsItBack()
    {
        await using var ctx = TestDb.CreateContext();
        var svc = new CollabService(ctx);

        Assert.Null(await svc.GetStateAsync("task:none"));

        await svc.SaveStateAsync("task:doc", new byte[] { 1, 2, 3 });
        Assert.Equal(new byte[] { 1, 2, 3 }, await svc.GetStateAsync("task:doc"));

        // A second save replaces the snapshot rather than inserting a duplicate.
        await svc.SaveStateAsync("task:doc", new byte[] { 4, 5 });
        Assert.Equal(new byte[] { 4, 5 }, await svc.GetStateAsync("task:doc"));
        Assert.Single(ctx.CollabDocuments);
    }
}
