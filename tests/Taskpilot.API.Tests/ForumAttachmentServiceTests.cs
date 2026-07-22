using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Taskpilot.API.Data;
using Taskpilot.API.Models;
using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>
/// Tests for <see cref="ForumAttachmentService"/> — files attached to forum topics, the last
/// part of spec module 7. A real <see cref="FileService"/> runs over an in-memory storage fake,
/// so these cover the whole path including the deletion rules that keep storage from leaking.
/// </summary>
public class ForumAttachmentServiceTests
{
    private readonly FakeStorage _storage = new();

    private ForumAttachmentService Create(TaskpilotDbContext ctx) =>
        new(ctx,
            new FileService(ctx, _storage, NullLogger<FileService>.Instance),
            NullLogger<ForumAttachmentService>.Instance);

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

    /// <summary>Seeds an author and one topic; returns their ids.</summary>
    private static async Task<(Guid author, Guid topicId)> SetupAsync(TaskpilotDbContext ctx, bool locked = false)
    {
        var author = await TestDb.AddUserAsync(ctx, "Author");
        var topicId = Guid.NewGuid();
        ctx.ForumTopics.Add(new ForumTopic
        {
            Id = topicId,
            Title = "How do I deploy this?",
            Body = "Logs attached.",
            AuthorId = author,
            IsLocked = locked,
        });
        await ctx.SaveChangesAsync();
        return (author, topicId);
    }

    [Fact]
    public async Task Attach_StoresTheFileAndLinksItToTheTopic()
    {
        await using var ctx = TestDb.CreateContext();
        var (author, topicId) = await SetupAsync(ctx);
        var svc = Create(ctx);

        var result = await svc.AttachAsync(author, topicId, FileOf("error.log"));

        Assert.True(result.Succeeded);
        Assert.Equal("error.log", result.Value!.FileName);
        Assert.Equal("Author", result.Value.UploadedByName);
        Assert.Equal(1, await ctx.ForumAttachments.CountAsync(a => a.TopicId == topicId));
        Assert.Equal(1, _storage.Count);   // the bytes really went to storage
    }

    [Fact]
    public async Task GetForTopic_ListsAttachments_ForAnySignedInUser()
    {
        await using var ctx = TestDb.CreateContext();
        var (author, topicId) = await SetupAsync(ctx);
        var svc = Create(ctx);
        await svc.AttachAsync(author, topicId, FileOf("screenshot.png"));

        // The forum is readable by everyone, so listing takes no user at all.
        var result = await svc.GetForTopicAsync(topicId);

        Assert.True(result.Succeeded);
        Assert.Single(result.Value!);
        Assert.Equal("screenshot.png", result.Value![0].FileName);
    }

    [Fact]
    public async Task Attach_ToSomeoneElsesTopic_IsRefused()
    {
        await using var ctx = TestDb.CreateContext();
        var (_, topicId) = await SetupAsync(ctx);
        var stranger = await TestDb.AddUserAsync(ctx, "Stranger");
        var svc = Create(ctx);

        // Attaching shows the file as part of the post, so it is editing someone's post.
        var result = await svc.AttachAsync(stranger, topicId, FileOf("spam.txt"));

        Assert.False(result.Succeeded);
        Assert.Equal("You can only attach files to your own topics.", result.Error);
        Assert.Equal(0, _storage.Count);
    }

    [Fact]
    public async Task Attach_ToALockedTopic_IsRefused()
    {
        await using var ctx = TestDb.CreateContext();
        var (author, topicId) = await SetupAsync(ctx, locked: true);
        var svc = Create(ctx);

        // A locked topic takes no new replies, so it takes no new files either.
        var result = await svc.AttachAsync(author, topicId, FileOf("late.txt"));

        Assert.False(result.Succeeded);
        Assert.Equal("This topic is locked.", result.Error);
        Assert.Equal(0, _storage.Count);
    }

    [Fact]
    public async Task Attach_ByAMutedUser_IsRefused()
    {
        await using var ctx = TestDb.CreateContext();
        var (author, topicId) = await SetupAsync(ctx);
        var user = await ctx.Users.FirstAsync(u => u.Id == author);
        user.MutedUntil = DateTime.UtcNow.AddHours(1);
        await ctx.SaveChangesAsync();
        var svc = Create(ctx);

        // A mute silences every forum write path; uploading a file is one of them.
        var result = await svc.AttachAsync(author, topicId, FileOf("evasion.txt"));

        Assert.False(result.Succeeded);
        Assert.Contains("muted", result.Error!);
        Assert.Equal(0, _storage.Count);
    }

    [Fact]
    public async Task Detach_RemovesTheLinkAndTheFile_LeavingNothingBehind()
    {
        await using var ctx = TestDb.CreateContext();
        var (author, topicId) = await SetupAsync(ctx);
        var svc = Create(ctx);
        var attached = (await svc.AttachAsync(author, topicId, FileOf("temp.txt"))).Value!;

        var result = await svc.DetachAsync(author, attached.Id);

        Assert.True(result.Succeeded);
        Assert.Equal(0, await ctx.ForumAttachments.CountAsync());
        // The point of the feature: no orphaned file row and no orphaned bytes.
        Assert.False(await ctx.FileAttachments.AnyAsync(f => f.Id == attached.FileId));
        Assert.Equal(0, _storage.Count);
    }

    [Fact]
    public async Task Detach_BySomeoneElse_IsRefused()
    {
        await using var ctx = TestDb.CreateContext();
        var (author, topicId) = await SetupAsync(ctx);
        var stranger = await TestDb.AddUserAsync(ctx, "Stranger");
        var svc = Create(ctx);
        var attached = (await svc.AttachAsync(author, topicId, FileOf("owned.txt"))).Value!;

        var result = await svc.DetachAsync(stranger, attached.Id);

        Assert.False(result.Succeeded);
        Assert.Equal("Only the person who attached this file can remove it.", result.Error);
        Assert.Equal(1, _storage.Count);
    }

    [Fact]
    public async Task DeleteAllForTopic_RemovesEveryFile_SoDeletingATopicLeaksNothing()
    {
        await using var ctx = TestDb.CreateContext();
        var (author, topicId) = await SetupAsync(ctx);
        var svc = Create(ctx);
        await svc.AttachAsync(author, topicId, FileOf("a.txt"));
        await svc.AttachAsync(author, topicId, FileOf("b.txt"));
        Assert.Equal(2, _storage.Count);

        await svc.DeleteAllForTopicAsync(topicId);

        Assert.Equal(0, await ctx.ForumAttachments.CountAsync());
        Assert.Equal(0, await ctx.FileAttachments.CountAsync());
        Assert.Equal(0, _storage.Count);
    }

    [Fact]
    public async Task DeletingATopic_CleansUpItsAttachments()
    {
        // The wiring itself: ForumService must call into this service, otherwise the links
        // cascade away and the files stay in storage forever.
        await using var ctx = TestDb.CreateContext();
        var (author, topicId) = await SetupAsync(ctx);
        var attachments = Create(ctx);
        await attachments.AttachAsync(author, topicId, FileOf("evidence.log"));

        var forum = new ForumService(ctx, new Mock<INotificationService>().Object,
            new Mock<IReputationService>().Object, attachments, NullLogger<ForumService>.Instance);
        var result = await forum.DeleteTopicAsync(topicId, author, isAdmin: false);

        Assert.True(result.Succeeded);
        Assert.Equal(0, await ctx.ForumAttachments.CountAsync());
        Assert.Equal(0, await ctx.FileAttachments.CountAsync());
        Assert.Equal(0, _storage.Count);
    }
}
