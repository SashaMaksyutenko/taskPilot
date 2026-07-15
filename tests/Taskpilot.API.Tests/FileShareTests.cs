using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Taskpilot.API.Data;
using Taskpilot.API.Models;
using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>
/// Unit tests for the file share-link flow in <see cref="FileService"/>: creating,
/// resolving and revoking a public token. The bytes live in an in-memory
/// <see cref="IFileStorage"/> fake, so these tests say nothing about which backend
/// (disk or S3) is in use — that is <see cref="LocalFileStorageTests"/>' job.
/// </summary>
public class FileShareTests
{
    private readonly FakeStorage _storage = new();

    private FileService Create(TaskpilotDbContext ctx) =>
        new(ctx, _storage, NullLogger<FileService>.Instance);

    /// <summary>Seeds a file row plus its bytes in storage so downloads can resolve.</summary>
    private async Task<Guid> SeedFileAsync(TaskpilotDbContext ctx, Guid uploaderId)
    {
        var id = Guid.NewGuid();
        var storedName = $"{id:N}.txt";
        ctx.FileAttachments.Add(new FileAttachment
        {
            Id = id,
            FileName = "notes.txt",
            StoredName = storedName,
            ContentType = "text/plain",
            SizeBytes = 5,
            UploaderId = uploaderId,
        });
        await ctx.SaveChangesAsync();

        await _storage.SaveAsync(storedName, new MemoryStream("hello"u8.ToArray()), "text/plain");
        return id;
    }

    /// <summary>An in-memory storage backend; keeps the tests off the disk entirely.</summary>
    private sealed class FakeStorage : IFileStorage
    {
        private readonly Dictionary<string, byte[]> _objects = new();

        public string Name => "fake";

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

    [Fact]
    public async Task Share_ByUploader_ReturnsStableToken()
    {
        await using var ctx = TestDb.CreateContext();
        var uploader = await TestDb.AddUserAsync(ctx, "Uploader");
        var svc = Create(ctx);
        var fileId = await SeedFileAsync(ctx, uploader);

        var first = await svc.CreateShareTokenAsync(fileId, uploader);
        var second = await svc.CreateShareTokenAsync(fileId, uploader);

        Assert.True(first.Succeeded);
        Assert.False(string.IsNullOrWhiteSpace(first.Value));
        // Sharing again returns the same link rather than rotating it.
        Assert.Equal(first.Value, second.Value);
    }

    [Fact]
    public async Task Share_ByNonUploader_Fails()
    {
        await using var ctx = TestDb.CreateContext();
        var uploader = await TestDb.AddUserAsync(ctx, "Uploader");
        var other = await TestDb.AddUserAsync(ctx, "Other");
        var svc = Create(ctx);
        var fileId = await SeedFileAsync(ctx, uploader);

        var result = await svc.CreateShareTokenAsync(fileId, other);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task SharedToken_ResolvesForAnonymousDownload()
    {
        await using var ctx = TestDb.CreateContext();
        var uploader = await TestDb.AddUserAsync(ctx, "Uploader");
        var svc = Create(ctx);
        var fileId = await SeedFileAsync(ctx, uploader);
        var token = (await svc.CreateShareTokenAsync(fileId, uploader)).Value!;

        var download = await svc.GetForDownloadByTokenAsync(token);

        Assert.True(download.Succeeded);
        Assert.Equal("notes.txt", download.Value!.FileName);
    }

    [Fact]
    public async Task Revoke_InvalidatesToken()
    {
        await using var ctx = TestDb.CreateContext();
        var uploader = await TestDb.AddUserAsync(ctx, "Uploader");
        var svc = Create(ctx);
        var fileId = await SeedFileAsync(ctx, uploader);
        var token = (await svc.CreateShareTokenAsync(fileId, uploader)).Value!;

        Assert.True((await svc.RevokeShareAsync(fileId, uploader)).Succeeded);

        // The old token no longer resolves.
        Assert.False((await svc.GetForDownloadByTokenAsync(token)).Succeeded);
    }

    [Fact]
    public async Task Revoke_ByNonUploader_Fails()
    {
        await using var ctx = TestDb.CreateContext();
        var uploader = await TestDb.AddUserAsync(ctx, "Uploader");
        var other = await TestDb.AddUserAsync(ctx, "Other");
        var svc = Create(ctx);
        var fileId = await SeedFileAsync(ctx, uploader);
        await svc.CreateShareTokenAsync(fileId, uploader);

        Assert.False((await svc.RevokeShareAsync(fileId, other)).Succeeded);
    }

    [Fact]
    public async Task UnknownToken_Fails()
    {
        await using var ctx = TestDb.CreateContext();
        var svc = Create(ctx);

        Assert.False((await svc.GetForDownloadByTokenAsync("nope")).Succeeded);
        Assert.False((await svc.GetForDownloadByTokenAsync("")).Succeeded);
    }
}
