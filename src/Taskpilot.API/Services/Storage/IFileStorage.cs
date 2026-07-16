namespace Taskpilot.API.Services;

/// <summary>
/// Where uploaded file bytes physically live. The database only ever stores metadata
/// plus a <c>storedName</c> key; this abstraction owns the bytes behind that key.
///
/// Two implementations exist: the local disk (fine for development) and any
/// S3-compatible bucket (Cloudflare R2, Backblaze B2, MinIO, AWS S3). The S3 one is
/// what makes deployment possible at all — hosting platforms give you an *ephemeral*
/// filesystem, so anything written to disk vanishes on the next restart.
/// </summary>
public interface IFileStorage
{
    /// <summary>Human-readable name of the active backend (for logs and /health).</summary>
    string Name { get; }

    /// <summary>Writes the bytes under <paramref name="storedName"/>.</summary>
    Task SaveAsync(string storedName, Stream content, string contentType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens the stored bytes for reading, or null when the key holds nothing (the row
    /// exists but the bytes are gone — a real possibility after a bad deploy).
    /// </summary>
    Task<Stream?> OpenReadAsync(string storedName, CancellationToken cancellationToken = default);

    /// <summary>Removes the bytes; a missing key is not an error.</summary>
    Task DeleteAsync(string storedName, CancellationToken cancellationToken = default);
}
