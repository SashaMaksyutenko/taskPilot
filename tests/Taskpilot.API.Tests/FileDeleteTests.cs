using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Taskpilot.API.Data;
using Taskpilot.API.Models;
using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>
/// Unit tests for permanent file deletion in <see cref="FileService"/>: who may do it,
/// what blocks it, and that the bytes go with the row.
/// The bytes live in an in-memory <see cref="IFileStorage"/> fake, so nothing touches disk.
/// </summary>
public class FileDeleteTests
{
    private readonly FakeStorage _storage = new();

    private FileService Create(TaskpilotDbContext ctx) =>
        new(ctx, _storage, NullLogger<FileService>.Instance);

    /// <summary>Seeds a file row plus its bytes in storage.</summary>
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
    private class FakeStorage : IFileStorage
    {
        protected readonly Dictionary<string, byte[]> Objects = new();

        public string Name => "fake";

        public bool Has(string storedName) => Objects.ContainsKey(storedName);

        public async Task SaveAsync(string storedName, Stream content, string contentType, CancellationToken cancellationToken = default)
        {
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken);
            Objects[storedName] = buffer.ToArray();
        }

        public Task<Stream?> OpenReadAsync(string storedName, CancellationToken cancellationToken = default) =>
            Task.FromResult<Stream?>(Objects.TryGetValue(storedName, out var bytes) ? new MemoryStream(bytes) : null);

        public virtual Task DeleteAsync(string storedName, CancellationToken cancellationToken = default)
        {
            Objects.Remove(storedName);
            return Task.CompletedTask;
        }
    }

    /// <summary>Storage whose delete always blows up — models an S3 outage.</summary>
    private sealed class ThrowingDeleteStorage : FakeStorage
    {
        public override Task DeleteAsync(string storedName, CancellationToken cancellationToken = default) =>
            throw new IOException("backend is down");
    }

    [Fact]
    public async Task Delete_ByUploader_RemovesRowAndBytes()
    {
        await using var ctx = TestDb.CreateContext();
        var uploader = await TestDb.AddUserAsync(ctx, "Uploader");
        var svc = Create(ctx);
        var fileId = await SeedFileAsync(ctx, uploader);
        var storedName = (await ctx.FileAttachments.AsNoTracking().FirstAsync(f => f.Id == fileId)).StoredName;

        var result = await svc.DeleteAsync(fileId, uploader);

        Assert.True(result.Succeeded);
        Assert.False(await ctx.FileAttachments.AnyAsync(f => f.Id == fileId));
        // The bytes must go too, or the quota this unblocks would never drop.
        Assert.False(_storage.Has(storedName));
    }

    [Fact]
    public async Task Delete_ByNonUploader_IsRefusedAndKeepsTheFile()
    {
        await using var ctx = TestDb.CreateContext();
        var uploader = await TestDb.AddUserAsync(ctx, "Uploader");
        var other = await TestDb.AddUserAsync(ctx, "Other");
        var svc = Create(ctx);
        var fileId = await SeedFileAsync(ctx, uploader);

        var result = await svc.DeleteAsync(fileId, other);

        Assert.False(result.Succeeded);
        Assert.Equal("Only the uploader can delete this file.", result.Error);
        Assert.True(await ctx.FileAttachments.AnyAsync(f => f.Id == fileId));
    }

    [Fact]
    public async Task Delete_OfAFileAttachedToAMessage_IsRefused()
    {
        await using var ctx = TestDb.CreateContext();
        var uploader = await TestDb.AddUserAsync(ctx, "Uploader");
        var svc = Create(ctx);
        var fileId = await SeedFileAsync(ctx, uploader);

        // Message.FileAttachmentId is a Restrict FK in Postgres; the service checks for
        // it explicitly so this fails with a reason rather than a database exception.
        ctx.Messages.Add(new Message
        {
            Id = Guid.NewGuid(),
            ConversationId = Guid.NewGuid(),
            SenderId = uploader,
            Content = "here you go",
            FileAttachmentId = fileId,
        });
        await ctx.SaveChangesAsync();

        var result = await svc.DeleteAsync(fileId, uploader);

        Assert.False(result.Succeeded);
        Assert.Equal("This file is attached to a chat message and cannot be deleted.", result.Error);
        Assert.True(await ctx.FileAttachments.AnyAsync(f => f.Id == fileId));
    }

    [Fact]
    public async Task Delete_OfAnAvatar_ClearsThePointerSoTheProfileDoesNotBreak()
    {
        await using var ctx = TestDb.CreateContext();
        var uploader = await TestDb.AddUserAsync(ctx, "Uploader");
        var svc = Create(ctx);
        var fileId = await SeedFileAsync(ctx, uploader);

        // AvatarFileId has no foreign key, so nothing else would clear it.
        var user = await ctx.Users.FirstAsync(u => u.Id == uploader);
        user.AvatarFileId = fileId;
        await ctx.SaveChangesAsync();

        var result = await svc.DeleteAsync(fileId, uploader);

        Assert.True(result.Succeeded);
        Assert.Null((await ctx.Users.FirstAsync(u => u.Id == uploader)).AvatarFileId);
    }

    [Fact]
    public async Task Delete_OfAMissingFile_Fails()
    {
        await using var ctx = TestDb.CreateContext();
        var uploader = await TestDb.AddUserAsync(ctx, "Uploader");
        var svc = Create(ctx);

        var result = await svc.DeleteAsync(Guid.NewGuid(), uploader);

        Assert.False(result.Succeeded);
        Assert.Equal("File not found.", result.Error);
    }

    [Fact]
    public async Task Delete_SucceedsEvenIfTheStorageBackendCannotRemoveTheBytes()
    {
        await using var ctx = TestDb.CreateContext();
        var uploader = await TestDb.AddUserAsync(ctx, "Uploader");
        var storage = new ThrowingDeleteStorage();
        var svc = new FileService(ctx, storage, NullLogger<FileService>.Instance);

        var id = Guid.NewGuid();
        ctx.FileAttachments.Add(new FileAttachment
        {
            Id = id,
            FileName = "notes.txt",
            StoredName = $"{id:N}.txt",
            ContentType = "text/plain",
            SizeBytes = 5,
            UploaderId = uploader,
        });
        await ctx.SaveChangesAsync();

        var result = await svc.DeleteAsync(id, uploader);

        // The row is already committed when the bytes fail, so the caller sees success
        // and the orphaned bytes are logged rather than resurrecting a deleted file.
        Assert.True(result.Succeeded);
        Assert.False(await ctx.FileAttachments.AnyAsync(f => f.Id == id));
    }
}
