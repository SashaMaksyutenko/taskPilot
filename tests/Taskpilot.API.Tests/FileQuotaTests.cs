using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Taskpilot.API.Data;
using Taskpilot.API.Models;
using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>
/// Tests that <see cref="FileService.SaveAsync"/> enforces the admin-editable storage
/// limits: the per-file cap and the organization-wide quota, both read from the settings
/// row (or the model defaults when the row is absent).
/// </summary>
public class FileQuotaTests
{
    private readonly FakeStorage _storage = new();

    private FileService Create(TaskpilotDbContext ctx) =>
        new(ctx, _storage, NullLogger<FileService>.Instance);

    /// <summary>Seeds the singleton settings row with the given limits.</summary>
    private static async Task SeedSettingsAsync(TaskpilotDbContext ctx, long maxUpload, long quota)
    {
        ctx.OrganizationSettings.Add(new OrganizationSettings
        {
            Id = OrganizationSettings.SingletonId,
            MaxUploadBytes = maxUpload,
            StorageQuotaBytes = quota,
        });
        await ctx.SaveChangesAsync();
    }

    /// <summary>Adds an existing stored file of the given size so usage totals add up.</summary>
    private static async Task AddExistingFileAsync(TaskpilotDbContext ctx, Guid uploaderId, long sizeBytes)
    {
        var id = Guid.NewGuid();
        ctx.FileAttachments.Add(new FileAttachment
        {
            Id = id,
            FileName = "f.bin",
            StoredName = $"{id:N}.bin",
            ContentType = "application/octet-stream",
            SizeBytes = sizeBytes,
            UploaderId = uploaderId,
        });
        await ctx.SaveChangesAsync();
    }

    /// <summary>Builds an uploaded file of an exact byte length.</summary>
    private static IFormFile FileOfSize(int bytes)
    {
        var buffer = new byte[bytes];
        return new FormFile(new MemoryStream(buffer), 0, bytes, "file", "upload.bin")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/octet-stream",
        };
    }

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

    [Fact]
    public async Task Upload_OverThePerFileLimit_IsRejected()
    {
        await using var ctx = TestDb.CreateContext();
        var uploader = await TestDb.AddUserAsync(ctx);
        await SeedSettingsAsync(ctx, maxUpload: 1_000, quota: 1_000_000);
        var svc = Create(ctx);

        var result = await svc.SaveAsync(FileOfSize(1_500), uploader);

        Assert.False(result.Succeeded);
        Assert.Contains("limit", result.Error);
        // Nothing was written to storage or the database.
        Assert.Equal(0, _storage.Count);
        Assert.False(await ctx.FileAttachments.AnyAsync());
    }

    [Fact]
    public async Task Upload_OverAMegabyteScaleLimit_ReportsMegabytesNotZero()
    {
        await using var ctx = TestDb.CreateContext();
        var uploader = await TestDb.AddUserAsync(ctx);
        await SeedSettingsAsync(ctx, maxUpload: 2 * 1024 * 1024, quota: 1_000_000_000);
        var svc = Create(ctx);

        var result = await svc.SaveAsync(FileOfSize(3 * 1024 * 1024), uploader);

        Assert.False(result.Succeeded);
        // A realistic MB-scale limit must read "2 MB", never round down to "0 MB".
        Assert.Contains("2 MB", result.Error);
    }

    [Fact]
    public async Task Upload_ThatWouldExceedTheQuota_IsRejected()
    {
        await using var ctx = TestDb.CreateContext();
        var uploader = await TestDb.AddUserAsync(ctx);
        await SeedSettingsAsync(ctx, maxUpload: 1_000_000, quota: 10_000);
        // 9,500 of 10,000 bytes already used, so a 600-byte upload would overflow.
        await AddExistingFileAsync(ctx, uploader, 9_500);
        var svc = Create(ctx);

        var result = await svc.SaveAsync(FileOfSize(600), uploader);

        Assert.False(result.Succeeded);
        Assert.Equal("The organization's storage quota has been reached.", result.Error);
        Assert.Equal(0, _storage.Count);
        // The pre-existing file is still the only row.
        Assert.Equal(1, await ctx.FileAttachments.CountAsync());
    }

    [Fact]
    public async Task Upload_ExactlyFillingTheQuota_IsAllowed()
    {
        await using var ctx = TestDb.CreateContext();
        var uploader = await TestDb.AddUserAsync(ctx);
        await SeedSettingsAsync(ctx, maxUpload: 1_000_000, quota: 10_000);
        await AddExistingFileAsync(ctx, uploader, 9_400);
        var svc = Create(ctx);

        // 9,400 + 600 == 10,000, which is not over the quota.
        var result = await svc.SaveAsync(FileOfSize(600), uploader);

        Assert.True(result.Succeeded);
        Assert.Equal(2, await ctx.FileAttachments.CountAsync());
    }

    [Fact]
    public async Task Upload_WithNoSettingsRow_UsesTheModelDefaults()
    {
        await using var ctx = TestDb.CreateContext();
        var uploader = await TestDb.AddUserAsync(ctx);
        var svc = Create(ctx);

        // No settings seeded: a small file is well under the 10 MB / 1 GB defaults.
        var result = await svc.SaveAsync(FileOfSize(2_048), uploader);

        Assert.True(result.Succeeded);
        Assert.Equal(1, await ctx.FileAttachments.CountAsync());
    }
}
