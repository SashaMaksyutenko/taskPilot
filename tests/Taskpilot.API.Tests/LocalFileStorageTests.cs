using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>
/// Unit tests for <see cref="LocalFileStorage"/> — the disk backend used whenever no
/// S3 bucket is configured. Runs against a throwaway content root.
/// </summary>
public class LocalFileStorageTests : IDisposable
{
    private readonly string _contentRoot =
        Path.Combine(Path.GetTempPath(), "taskpilot-tests", Guid.NewGuid().ToString("N"));

    private readonly LocalFileStorage _storage;

    public LocalFileStorageTests()
    {
        var env = new Mock<IWebHostEnvironment>();
        env.SetupGet(e => e.ContentRootPath).Returns(_contentRoot);
        _storage = new LocalFileStorage(env.Object, NullLogger<LocalFileStorage>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_contentRoot))
            Directory.Delete(_contentRoot, recursive: true);
    }

    [Fact]
    public async Task SaveThenRead_RoundTripsTheBytes()
    {
        await _storage.SaveAsync("a.txt", new MemoryStream("hello"u8.ToArray()), "text/plain");

        await using var read = await _storage.OpenReadAsync("a.txt");

        Assert.NotNull(read);
        using var reader = new StreamReader(read!);
        Assert.Equal("hello", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task OpenRead_MissingKey_ReturnsNull()
    {
        Assert.Null(await _storage.OpenReadAsync("nope.txt"));
    }

    [Fact]
    public async Task Delete_RemovesTheBytes_AndIsSafeWhenMissing()
    {
        await _storage.SaveAsync("b.txt", new MemoryStream("bye"u8.ToArray()), "text/plain");

        await _storage.DeleteAsync("b.txt");
        Assert.Null(await _storage.OpenReadAsync("b.txt"));

        // Deleting something that is already gone must not throw.
        await _storage.DeleteAsync("b.txt");
    }

    [Fact]
    public async Task RejectsKeysThatTryToEscapeTheUploadsFolder()
    {
        // A crafted key must never be able to write outside the uploads directory.
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _storage.SaveAsync("../escape.txt", new MemoryStream("x"u8.ToArray()), "text/plain"));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _storage.OpenReadAsync("../../secrets.env"));
    }
}
