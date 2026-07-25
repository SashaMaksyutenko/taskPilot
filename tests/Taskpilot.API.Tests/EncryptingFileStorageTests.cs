using System.Text;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using Taskpilot.API.Common;
using Taskpilot.API.Configuration;
using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>
/// Tests the <see cref="EncryptingFileStorage"/> decorator (spec module 18): it encrypts bytes
/// before they reach the real backend, decrypts them on the way out, and serves pre-encryption
/// (plaintext) files unchanged so enabling encryption never breaks existing uploads.
/// </summary>
public class EncryptingFileStorageTests
{
    /// <summary>An in-memory <see cref="IFileStorage"/> standing in for the disk or a bucket.</summary>
    private sealed class FakeStorage : IFileStorage
    {
        public readonly Dictionary<string, byte[]> Blobs = new();
        public int Deleted;
        public string Name => "fake";

        public Task SaveAsync(string storedName, Stream content, string contentType, CancellationToken ct = default)
        {
            using var ms = new MemoryStream();
            content.CopyTo(ms);
            Blobs[storedName] = ms.ToArray();
            return Task.CompletedTask;
        }

        public Task<Stream?> OpenReadAsync(string storedName, CancellationToken ct = default) =>
            Task.FromResult<Stream?>(Blobs.TryGetValue(storedName, out var b) ? new MemoryStream(b) : null);

        public Task DeleteAsync(string storedName, CancellationToken ct = default)
        {
            if (Blobs.Remove(storedName)) Deleted++;
            return Task.CompletedTask;
        }
    }

    private static (EncryptingFileStorage svc, FakeStorage inner) Create()
    {
        var inner = new FakeStorage();
        var options = new StorageOptions { EncryptionKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) };
        return (new EncryptingFileStorage(inner, options, NullLogger<EncryptingFileStorage>.Instance), inner);
    }

    private static async Task<string> ReadAllAsync(Stream? s)
    {
        Assert.NotNull(s);
        using var reader = new StreamReader(s!);
        return await reader.ReadToEndAsync();
    }

    [Fact]
    public async Task Save_StoresEncryptedBytes_NotThePlaintext()
    {
        var (svc, inner) = Create();
        var plaintext = "confidential contract text";

        await svc.SaveAsync("f1", new MemoryStream(Encoding.UTF8.GetBytes(plaintext)), "text/plain");

        var stored = inner.Blobs["f1"];
        Assert.True(FileCipher.LooksEncrypted(stored));
        Assert.DoesNotContain(plaintext, Encoding.UTF8.GetString(stored));
    }

    [Fact]
    public async Task Save_ThenOpenRead_ReturnsTheOriginalContent()
    {
        var (svc, _) = Create();
        var plaintext = "round-trip me";

        await svc.SaveAsync("f1", new MemoryStream(Encoding.UTF8.GetBytes(plaintext)), "text/plain");
        var read = await ReadAllAsync(await svc.OpenReadAsync("f1"));

        Assert.Equal(plaintext, read);
    }

    [Fact]
    public async Task OpenRead_LegacyPlaintextFile_IsServedUnchanged()
    {
        var (svc, inner) = Create();
        // A file written before encryption was enabled: raw bytes, no cipher marker.
        inner.Blobs["old"] = Encoding.UTF8.GetBytes("pre-encryption content");

        var read = await ReadAllAsync(await svc.OpenReadAsync("old"));

        Assert.Equal("pre-encryption content", read);
    }

    [Fact]
    public async Task OpenRead_MissingFile_ReturnsNull()
    {
        var (svc, _) = Create();
        Assert.Null(await svc.OpenReadAsync("nope"));
    }

    [Fact]
    public async Task Delete_DelegatesToTheInnerStorage()
    {
        var (svc, inner) = Create();
        await svc.SaveAsync("f1", new MemoryStream(Encoding.UTF8.GetBytes("x")), "text/plain");

        await svc.DeleteAsync("f1");

        Assert.Equal(1, inner.Deleted);
        Assert.False(inner.Blobs.ContainsKey("f1"));
    }
}
