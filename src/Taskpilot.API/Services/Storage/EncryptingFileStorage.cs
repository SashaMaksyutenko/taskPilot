using System.Security.Cryptography;
using Taskpilot.API.Common;
using Taskpilot.API.Configuration;

namespace Taskpilot.API.Services;

/// <summary>
/// An <see cref="IFileStorage"/> decorator that encrypts file bytes at rest with AES-256-GCM
/// (see <see cref="FileCipher"/>). It wraps the real backend (disk or S3) so encryption is
/// transparent to the rest of the app: bytes are encrypted on the way in and decrypted on the
/// way out. Files stored before encryption was enabled are detected as plaintext and passed
/// through unchanged, so turning encryption on does not break existing uploads.
///
/// Registered only when <see cref="StorageOptions.EncryptionEnabled"/> is true.
/// </summary>
public class EncryptingFileStorage : IFileStorage
{
    private readonly IFileStorage _inner;
    private readonly byte[] _key;
    private readonly ILogger<EncryptingFileStorage> _logger;

    public EncryptingFileStorage(IFileStorage inner, StorageOptions options, ILogger<EncryptingFileStorage> logger)
    {
        _inner = inner;
        _logger = logger;
        _key = FileCipher.ParseKey(options.EncryptionKey);
        _logger.LogInformation("File encryption at rest enabled (AES-256-GCM), wrapping {Backend}.", inner.Name);
    }

    /// <inheritdoc />
    public string Name => $"{_inner.Name}+aes256gcm";

    /// <inheritdoc />
    public async Task SaveAsync(string storedName, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        // Buffer the upload (already size-capped upstream), encrypt, then hand the cipher blob
        // to the real backend. GCM is not a streaming cipher, so a full buffer is required.
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        var encrypted = FileCipher.Encrypt(_key, buffer.ToArray());

        using var blob = new MemoryStream(encrypted);
        await _inner.SaveAsync(storedName, blob, contentType, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Stream?> OpenReadAsync(string storedName, CancellationToken cancellationToken = default)
    {
        var raw = await _inner.OpenReadAsync(storedName, cancellationToken);
        if (raw is null)
            return null;

        await using (raw)
        {
            using var buffer = new MemoryStream();
            await raw.CopyToAsync(buffer, cancellationToken);
            var bytes = buffer.ToArray();

            // Files written before encryption was turned on have no marker: serve them as-is.
            if (!FileCipher.LooksEncrypted(bytes))
                return new MemoryStream(bytes);

            try
            {
                return new MemoryStream(FileCipher.Decrypt(_key, bytes));
            }
            catch (CryptographicException ex)
            {
                // Wrong key or corrupted/tampered bytes — refuse rather than serve garbage.
                _logger.LogError(ex, "Failed to decrypt stored file. StoredName: {StoredName}", storedName);
                return null;
            }
        }
    }

    /// <inheritdoc />
    public Task DeleteAsync(string storedName, CancellationToken cancellationToken = default) =>
        _inner.DeleteAsync(storedName, cancellationToken);
}
