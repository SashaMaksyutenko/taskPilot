using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Taskpilot.API.Data;
using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>
/// Tests for file version history in <see cref="FileService"/>: saving a new version,
/// listing the chain, and tearing the whole chain down without leaking bytes.
/// A real service runs over an in-memory storage fake, so nothing touches disk.
/// </summary>
public class FileVersionTests
{
    private readonly FakeStorage _storage = new();

    private FileService Create(TaskpilotDbContext ctx) =>
        new(ctx, _storage, NullLogger<FileService>.Instance);

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

    [Fact]
    public async Task SaveVersion_NumbersTheNewFileAndChainsItToTheOld()
    {
        await using var ctx = TestDb.CreateContext();
        var user = await TestDb.AddUserAsync(ctx, "Owner");
        var svc = Create(ctx);
        var v1 = (await svc.SaveAsync(FileOf("plan.txt", "one"), user)).Value!;

        var v2 = (await svc.SaveVersionAsync(FileOf("plan.txt", "two"), user, v1.Id)).Value!;

        // The new row is a distinct file at version 2, pointing back at version 1.
        Assert.NotEqual(v1.Id, v2.Id);
        var stored = await ctx.FileAttachments.AsNoTracking().FirstAsync(f => f.Id == v2.Id);
        Assert.Equal(2, stored.Version);
        Assert.Equal(v1.Id, stored.PreviousVersionId);
        Assert.Equal(2, _storage.Count);   // both versions' bytes are kept
    }

    [Fact]
    public async Task SaveVersion_OfAMissingFile_Fails()
    {
        await using var ctx = TestDb.CreateContext();
        var user = await TestDb.AddUserAsync(ctx, "Owner");
        var svc = Create(ctx);

        var result = await svc.SaveVersionAsync(FileOf("x.txt"), user, Guid.NewGuid());

        Assert.False(result.Succeeded);
        Assert.Equal("File not found.", result.Error);
    }

    [Fact]
    public async Task GetVersions_ReturnsTheWholeChain_NewestFirst_WithCurrentFlagged()
    {
        await using var ctx = TestDb.CreateContext();
        var user = await TestDb.AddUserAsync(ctx, "Owner");
        var svc = Create(ctx);
        var v1 = (await svc.SaveAsync(FileOf("doc.txt", "a"), user)).Value!;
        var v2 = (await svc.SaveVersionAsync(FileOf("doc.txt", "bb"), user, v1.Id)).Value!;
        var v3 = (await svc.SaveVersionAsync(FileOf("doc.txt", "ccc"), user, v2.Id)).Value!;

        var versions = (await svc.GetVersionsAsync(v3.Id)).Value!;

        Assert.Equal(new[] { 3, 2, 1 }, versions.Select(v => v.Version).ToArray());
        Assert.Equal("Owner", versions[0].UploadedByName);
        // Only the file the attachment points at is current.
        Assert.True(versions[0].IsCurrent);
        Assert.False(versions[1].IsCurrent);
        Assert.False(versions[2].IsCurrent);
    }

    [Fact]
    public async Task GetVersions_OfAFirstUpload_ReturnsJustThatOne()
    {
        await using var ctx = TestDb.CreateContext();
        var user = await TestDb.AddUserAsync(ctx, "Owner");
        var svc = Create(ctx);
        var v1 = (await svc.SaveAsync(FileOf("solo.txt"), user)).Value!;

        var versions = (await svc.GetVersionsAsync(v1.Id)).Value!;

        Assert.Single(versions);
        Assert.Equal(1, versions[0].Version);
        Assert.True(versions[0].IsCurrent);
    }

    [Fact]
    public async Task DeleteWithVersions_RemovesEveryVersion_RowsAndBytes()
    {
        await using var ctx = TestDb.CreateContext();
        var user = await TestDb.AddUserAsync(ctx, "Owner");
        var svc = Create(ctx);
        var v1 = (await svc.SaveAsync(FileOf("r.txt", "a"), user)).Value!;
        var v2 = (await svc.SaveVersionAsync(FileOf("r.txt", "bb"), user, v1.Id)).Value!;
        var v3 = (await svc.SaveVersionAsync(FileOf("r.txt", "ccc"), user, v2.Id)).Value!;
        Assert.Equal(3, _storage.Count);

        var result = await svc.DeleteWithVersionsAsync(v3.Id, user);

        Assert.True(result.Succeeded);
        // The self-referencing Restrict FK must not block tearing the chain down.
        Assert.Equal(0, await ctx.FileAttachments.CountAsync());
        Assert.Equal(0, _storage.Count);
    }

    [Fact]
    public async Task DeleteWithVersions_ByeSomeoneElse_IsRefused_AndKeepsEverything()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var other = await TestDb.AddUserAsync(ctx, "Other");
        var svc = Create(ctx);
        var v1 = (await svc.SaveAsync(FileOf("r.txt", "a"), owner)).Value!;
        var v2 = (await svc.SaveVersionAsync(FileOf("r.txt", "bb"), owner, v1.Id)).Value!;

        var result = await svc.DeleteWithVersionsAsync(v2.Id, other);

        Assert.False(result.Succeeded);
        Assert.Equal("Only the uploader can delete this file.", result.Error);
        Assert.Equal(2, await ctx.FileAttachments.CountAsync());
        Assert.Equal(2, _storage.Count);
    }
}
