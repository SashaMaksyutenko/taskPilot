using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Taskpilot.API.Data;
using Taskpilot.API.Models;
using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>
/// Tests for <see cref="TaskAttachmentService"/> — files attached to tasks (spec module 7).
/// A real <see cref="FileService"/> runs over an in-memory storage fake, so these cover the
/// whole path: upload, link, list, and the deletion rules that keep storage from leaking.
/// </summary>
public class TaskAttachmentServiceTests
{
    private readonly FakeStorage _storage = new();

    private TaskAttachmentService Create(TaskpilotDbContext ctx) =>
        new(ctx,
            new FileService(ctx, _storage, NullLogger<FileService>.Instance),
            NullLogger<TaskAttachmentService>.Instance);

    /// <summary>An in-memory storage backend; keeps the tests off the disk entirely.</summary>
    private sealed class FakeStorage : IFileStorage
    {
        private readonly Dictionary<string, byte[]> _objects = new();

        public string Name => "fake";
        public int Count => _objects.Count;

        public async Task SaveAsync(string storedName, Stream content, string contentType, CancellationToken cancellationToken = default)
        {
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken);
            _objects[storedName] = buffer.ToArray();
        }

        public Task<Stream?> OpenReadAsync(string storedName, CancellationToken cancellationToken = default) =>
            Task.FromResult<Stream?>(_objects.TryGetValue(storedName, out var bytes) ? new MemoryStream(bytes) : null);

        public Task DeleteAsync(string storedName, CancellationToken cancellationToken = default)
        {
            _objects.Remove(storedName);
            return Task.CompletedTask;
        }
    }

    /// <summary>Builds an uploaded file of the given name and content.</summary>
    private static IFormFile FileOf(string name, string content = "hello")
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", name)
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/plain",
        };
    }

    /// <summary>Seeds an owner, a project and one task; returns their ids.</summary>
    private static async Task<(Guid owner, Guid projectId, Guid taskId)> SetupAsync(TaskpilotDbContext ctx)
    {
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var projectId = await TestDb.AddProjectAsync(ctx, owner);
        var taskId = Guid.NewGuid();
        ctx.ProjectTasks.Add(new ProjectTask
        {
            Id = taskId,
            ProjectId = projectId,
            Title = "Task",
            Status = ProjectTaskStatus.Backlog,
            Priority = TaskPriority.Medium,
            CreatorId = owner,
            Tags = new List<string>(),
        });
        await ctx.SaveChangesAsync();
        return (owner, projectId, taskId);
    }

    [Fact]
    public async Task Attach_StoresTheFileAndLinksItToTheTask()
    {
        await using var ctx = TestDb.CreateContext();
        var (owner, _, taskId) = await SetupAsync(ctx);
        var svc = Create(ctx);

        var result = await svc.AttachAsync(owner, taskId, FileOf("spec.txt"));

        Assert.True(result.Succeeded);
        Assert.Equal("spec.txt", result.Value!.FileName);
        Assert.Equal("Owner", result.Value.UploadedByName);
        Assert.Equal(1, await ctx.TaskAttachments.CountAsync(a => a.TaskId == taskId));
        Assert.Equal(1, _storage.Count);   // the bytes really went to storage
    }

    [Fact]
    public async Task GetForTask_ListsAttachments_ForAnyProjectParticipant()
    {
        await using var ctx = TestDb.CreateContext();
        var (owner, projectId, taskId) = await SetupAsync(ctx);
        var viewer = await TestDb.AddUserAsync(ctx, "Viewer");
        ctx.ProjectMembers.Add(new ProjectMember
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            UserId = viewer,
            Role = ProjectMemberRole.Viewer,
        });
        await ctx.SaveChangesAsync();
        var svc = Create(ctx);
        await svc.AttachAsync(owner, taskId, FileOf("notes.txt"));

        // Reading is a read: a Viewer may see attachments even though they cannot add any.
        var result = await svc.GetForTaskAsync(viewer, taskId);

        Assert.True(result.Succeeded);
        Assert.Single(result.Value!);
        Assert.Equal("notes.txt", result.Value![0].FileName);
    }

    [Fact]
    public async Task Attach_ByAViewer_IsRefused()
    {
        await using var ctx = TestDb.CreateContext();
        var (_, projectId, taskId) = await SetupAsync(ctx);
        var viewer = await TestDb.AddUserAsync(ctx, "Viewer");
        ctx.ProjectMembers.Add(new ProjectMember
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            UserId = viewer,
            Role = ProjectMemberRole.Viewer,
        });
        await ctx.SaveChangesAsync();
        var svc = Create(ctx);

        var result = await svc.AttachAsync(viewer, taskId, FileOf("sneaky.txt"));

        Assert.False(result.Succeeded);
        Assert.Equal("You have read-only access to this project.", result.Error);
        Assert.Equal(0, _storage.Count);
    }

    [Fact]
    public async Task Attach_ByAnOutsider_IsRefused()
    {
        await using var ctx = TestDb.CreateContext();
        var (_, _, taskId) = await SetupAsync(ctx);
        var outsider = await TestDb.AddUserAsync(ctx, "Outsider");
        var svc = Create(ctx);

        var result = await svc.AttachAsync(outsider, taskId, FileOf("nope.txt"));

        Assert.False(result.Succeeded);
        Assert.Equal("Task not found.", result.Error);
    }

    [Fact]
    public async Task Detach_RemovesTheLinkAndTheFile_LeavingNothingBehind()
    {
        await using var ctx = TestDb.CreateContext();
        var (owner, _, taskId) = await SetupAsync(ctx);
        var svc = Create(ctx);
        var attached = (await svc.AttachAsync(owner, taskId, FileOf("temp.txt"))).Value!;

        var result = await svc.DetachAsync(owner, attached.Id);

        Assert.True(result.Succeeded);
        Assert.Equal(0, await ctx.TaskAttachments.CountAsync());
        // The point of the feature: no orphaned file row and no orphaned bytes.
        Assert.False(await ctx.FileAttachments.AnyAsync(f => f.Id == attached.FileId));
        Assert.Equal(0, _storage.Count);
    }

    [Fact]
    public async Task Detach_ByeSomeoneElse_IsRefused()
    {
        await using var ctx = TestDb.CreateContext();
        var (owner, projectId, taskId) = await SetupAsync(ctx);
        var editor = await TestDb.AddUserAsync(ctx, "Editor");
        ctx.ProjectMembers.Add(new ProjectMember
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            UserId = editor,
            Role = ProjectMemberRole.Editor,
        });
        await ctx.SaveChangesAsync();
        var svc = Create(ctx);
        var attached = (await svc.AttachAsync(owner, taskId, FileOf("owned.txt"))).Value!;

        // An Editor can write to the project but did not attach this file.
        var result = await svc.DetachAsync(editor, attached.Id);

        Assert.False(result.Succeeded);
        Assert.Equal("Only the person who attached this file can remove it.", result.Error);
        Assert.Equal(1, _storage.Count);
    }

    [Fact]
    public async Task DeleteAllForTask_RemovesEveryFile_SoDeletingATaskLeaksNothing()
    {
        await using var ctx = TestDb.CreateContext();
        var (owner, _, taskId) = await SetupAsync(ctx);
        var svc = Create(ctx);
        await svc.AttachAsync(owner, taskId, FileOf("a.txt"));
        await svc.AttachAsync(owner, taskId, FileOf("b.txt"));
        Assert.Equal(2, _storage.Count);

        await svc.DeleteAllForTaskAsync(taskId);

        Assert.Equal(0, await ctx.TaskAttachments.CountAsync());
        Assert.Equal(0, await ctx.FileAttachments.CountAsync());
        Assert.Equal(0, _storage.Count);
    }
}
