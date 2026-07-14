using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Taskpilot.API.Data;
using Taskpilot.API.Models;
using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>
/// Unit tests for the file share-link flow in <see cref="FileService"/>: creating,
/// resolving and revoking a public token. Uses a throwaway content root on disk.
/// </summary>
public class FileShareTests : IDisposable
{
    private readonly string _contentRoot =
        Path.Combine(Path.GetTempPath(), "taskpilot-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_contentRoot))
            Directory.Delete(_contentRoot, recursive: true);
    }

    private FileService Create(TaskpilotDbContext ctx)
    {
        var env = new Mock<IWebHostEnvironment>();
        env.SetupGet(e => e.ContentRootPath).Returns(_contentRoot);
        return new FileService(ctx, env.Object, NullLogger<FileService>.Instance);
    }

    /// <summary>Seeds a file row plus its bytes on disk so downloads can resolve.</summary>
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

        var uploads = Path.Combine(_contentRoot, "uploads");
        Directory.CreateDirectory(uploads);
        await File.WriteAllTextAsync(Path.Combine(uploads, storedName), "hello");
        return id;
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
