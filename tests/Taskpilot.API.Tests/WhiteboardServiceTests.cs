using Microsoft.AspNetCore.SignalR;
using Moq;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Whiteboard;
using Taskpilot.API.Hubs;
using Taskpilot.API.Models;
using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>
/// Tests for the authoritative whiteboard: creation attribution, edit attribution, and — the
/// reason it exists — server-enforced deletion (only the author or the project owner).
/// </summary>
public class WhiteboardServiceTests
{
    private static WhiteboardService Make(TaskpilotDbContext ctx)
    {
        // A no-op hub context so broadcasts don't blow up.
        var proxy = new Mock<IClientProxy>();
        proxy.Setup(p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
             .Returns(Task.CompletedTask);
        var clients = new Mock<IHubClients>();
        clients.Setup(c => c.Group(It.IsAny<string>())).Returns(proxy.Object);
        var hub = new Mock<IHubContext<WhiteboardHub>>();
        hub.Setup(h => h.Clients).Returns(clients.Object);
        return new WhiteboardService(ctx, hub.Object);
    }

    private static async Task<(Guid owner, Guid editor, Guid other, Guid projectId)> SeedAsync(TaskpilotDbContext ctx)
    {
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var editor = await TestDb.AddUserAsync(ctx, "Editor");
        var other = await TestDb.AddUserAsync(ctx, "Other");
        var projectId = await TestDb.AddProjectAsync(ctx, owner);
        ctx.ProjectMembers.Add(new ProjectMember { Id = Guid.NewGuid(), ProjectId = projectId, UserId = editor, Role = ProjectMemberRole.Editor });
        ctx.ProjectMembers.Add(new ProjectMember { Id = Guid.NewGuid(), ProjectId = projectId, UserId = other, Role = ProjectMemberRole.Editor });
        await ctx.SaveChangesAsync();
        return (owner, editor, other, projectId);
    }

    [Fact]
    public async Task Create_StampsAuthor_AndStrangerCannot()
    {
        await using var ctx = TestDb.CreateContext();
        var (_, editor, _, projectId) = await SeedAsync(ctx);
        var stranger = await TestDb.AddUserAsync(ctx, "Stranger");
        var svc = Make(ctx);

        var created = await svc.CreateAsync(editor, projectId, new CreateNoteDto { X = 1, Y = 2, Text = "hi" });
        Assert.True(created.Succeeded);
        Assert.Equal(editor, created.Value!.AuthorId);
        Assert.Equal("Editor", created.Value.AuthorName);

        Assert.False((await svc.CreateAsync(stranger, projectId, new CreateNoteDto())).Succeeded);
    }

    [Fact]
    public async Task Delete_OnlyAuthorOrOwner()
    {
        await using var ctx = TestDb.CreateContext();
        var (owner, editor, other, projectId) = await SeedAsync(ctx);
        var svc = Make(ctx);
        var noteId = (await svc.CreateAsync(editor, projectId, new CreateNoteDto { Text = "mine" })).Value!.Id;

        // Another member (not the author, not the owner) is refused.
        var refused = await svc.DeleteAsync(other, noteId);
        Assert.False(refused.Succeeded);
        Assert.Equal("You can only delete your own notes.", refused.Error);
        Assert.True(ctx.WhiteboardNotes.Any(n => n.Id == noteId)); // still there

        // The author can delete their own.
        Assert.True((await svc.DeleteAsync(editor, noteId)).Succeeded);
        Assert.False(ctx.WhiteboardNotes.Any(n => n.Id == noteId));

        // The project owner can delete anyone's note.
        var note2 = (await svc.CreateAsync(editor, projectId, new CreateNoteDto { Text = "his" })).Value!.Id;
        Assert.True((await svc.DeleteAsync(owner, note2)).Succeeded);
    }

    [Fact]
    public async Task Delete_MissingNote_Fails()
    {
        await using var ctx = TestDb.CreateContext();
        var (owner, _, _, _) = await SeedAsync(ctx);

        var res = await Make(ctx).DeleteAsync(owner, Guid.NewGuid());
        Assert.False(res.Succeeded);
        Assert.Equal("Note not found.", res.Error);
    }

    [Fact]
    public async Task Update_SetsEditedBy_OnlyWhenEditorIsNotAuthor()
    {
        await using var ctx = TestDb.CreateContext();
        var (_, editor, other, projectId) = await SeedAsync(ctx);
        var svc = Make(ctx);
        var noteId = (await svc.CreateAsync(editor, projectId, new CreateNoteDto { Text = "v1" })).Value!.Id;

        // Author edits their own text → no "edited by".
        var byAuthor = await svc.UpdateAsync(editor, noteId, new UpdateNoteDto { Text = "v2" });
        Assert.Null(byAuthor.Value!.EditedById);

        // Someone else edits → attributed.
        var byOther = await svc.UpdateAsync(other, noteId, new UpdateNoteDto { Text = "v3" });
        Assert.Equal(other, byOther.Value!.EditedById);
        Assert.Equal("Other", byOther.Value.EditedByName);
    }
}
