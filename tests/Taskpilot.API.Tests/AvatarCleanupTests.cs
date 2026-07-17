using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Taskpilot.API.Data;
using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>
/// Tests that changing an avatar does not leak the old image. Replacing or removing an
/// avatar used to just move <c>User.AvatarFileId</c>, orphaning the previous file row and
/// its bytes forever — nothing else references an avatar, so nothing else would clean it.
/// These run a real <see cref="FileService"/> over an in-memory storage fake, so they
/// cover the whole upload-then-replace path.
/// </summary>
public class AvatarCleanupTests
{
    private readonly FakeStorage _storage = new();

    private UserService Create(TaskpilotDbContext ctx) =>
        new(ctx, new FileService(ctx, _storage, NullLogger<FileService>.Instance), NullLogger<UserService>.Instance);

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

    /// <summary>Builds an uploaded image the avatar endpoint will accept.</summary>
    private static IFormFile ImageFile(string name)
    {
        var bytes = "not-really-a-png"u8.ToArray();
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", name)
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/png",
        };
    }

    [Fact]
    public async Task ReplacingAnAvatar_DeletesThePreviousImage()
    {
        await using var ctx = TestDb.CreateContext();
        var userId = await TestDb.AddUserAsync(ctx);
        var svc = Create(ctx);

        await svc.SetAvatarAsync(userId, ImageFile("first.png"));
        var firstId = (await ctx.Users.AsNoTracking().FirstAsync(u => u.Id == userId)).AvatarFileId;

        await svc.SetAvatarAsync(userId, ImageFile("second.png"));
        var secondId = (await ctx.Users.AsNoTracking().FirstAsync(u => u.Id == userId)).AvatarFileId;

        Assert.NotNull(firstId);
        Assert.NotEqual(firstId, secondId);
        // The old row is gone...
        Assert.False(await ctx.FileAttachments.AnyAsync(f => f.Id == firstId));
        // ...the new one stays, and only its bytes remain in storage.
        Assert.True(await ctx.FileAttachments.AnyAsync(f => f.Id == secondId));
        Assert.Equal(1, _storage.Count);
    }

    [Fact]
    public async Task RemovingAnAvatar_DeletesTheImage()
    {
        await using var ctx = TestDb.CreateContext();
        var userId = await TestDb.AddUserAsync(ctx);
        var svc = Create(ctx);

        await svc.SetAvatarAsync(userId, ImageFile("first.png"));
        var fileId = (await ctx.Users.AsNoTracking().FirstAsync(u => u.Id == userId)).AvatarFileId;

        var result = await svc.RemoveAvatarAsync(userId);

        Assert.True(result.Succeeded);
        Assert.Null((await ctx.Users.AsNoTracking().FirstAsync(u => u.Id == userId)).AvatarFileId);
        Assert.False(await ctx.FileAttachments.AnyAsync(f => f.Id == fileId));
        Assert.Equal(0, _storage.Count);
    }

    [Fact]
    public async Task SettingTheFirstAvatar_HasNothingToCleanUp()
    {
        await using var ctx = TestDb.CreateContext();
        var userId = await TestDb.AddUserAsync(ctx);
        var svc = Create(ctx);

        var result = await svc.SetAvatarAsync(userId, ImageFile("first.png"));

        Assert.True(result.Succeeded);
        Assert.Equal(1, _storage.Count);
    }
}
