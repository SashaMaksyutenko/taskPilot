using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using Taskpilot.API.Configuration;

namespace Taskpilot.API.Services;

/// <summary>
/// Stores file bytes in an S3-compatible bucket (Cloudflare R2, Backblaze B2, MinIO,
/// AWS S3). Used whenever the storage options carry a bucket and keys — which is what
/// makes the app deployable, since hosting platforms wipe the local disk on restart.
///
/// Uses the AWS SDK rather than a hand-rolled HttpClient (unlike the Stripe/Giphy
/// clients here): S3 requires SigV4 request signing, and hand-writing that is a genuine
/// correctness risk for no benefit.
/// </summary>
public class S3FileStorage : IFileStorage, IDisposable
{
    private readonly IAmazonS3 _client;
    private readonly string _bucket;

    /// <summary>True when uploads may skip payload signing (HTTPS custom endpoints, i.e. R2).</summary>
    private readonly bool _disablePayloadSigning;
    private readonly ILogger<S3FileStorage> _logger;

    public S3FileStorage(IOptions<StorageOptions> options, ILogger<S3FileStorage> logger)
    {
        var settings = options.Value;
        _bucket = settings.Bucket;
        _logger = logger;

        var credentials = new BasicAWSCredentials(settings.AccessKey, settings.SecretKey);
        var config = new AmazonS3Config
        {
            // Non-AWS providers (R2, MinIO…) need path-style addressing.
            ForcePathStyle = true,
        };

        if (!string.IsNullOrWhiteSpace(settings.ServiceUrl))
        {
            config.ServiceURL = settings.ServiceUrl;
            // R2 and friends ignore the region but the SDK still wants one set.
            config.AuthenticationRegion = string.IsNullOrWhiteSpace(settings.Region) ? "auto" : settings.Region;

            _disablePayloadSigning = ResolvePayloadSigning(settings);
        }
        else
        {
            config.RegionEndpoint = RegionEndpoint.GetBySystemName(settings.Region);
        }

        _client = new AmazonS3Client(credentials, config);
        _logger.LogInformation("File storage: S3 bucket \"{Bucket}\", payload signing {Signing}.",
            _bucket, _disablePayloadSigning ? "disabled (R2)" : "enabled");
    }

    /// <summary>
    /// Decides whether uploads skip the chunked payload signature.
    /// <para>
    /// Cloudflare R2 rejects that signature and needs it skipped; Supabase, Backblaze B2,
    /// MinIO and AWS all expect the normal signed request. Guessing from the host keeps both
    /// working out of the box, and <see cref="StorageOptions.DisablePayloadSigning"/> overrides
    /// the guess if a provider disagrees.
    /// </para>
    /// It is never skipped over plain HTTP: the SDK refuses that combination outright
    /// ("When DisablePayloadSigning is true, the request must be sent over HTTPS").
    /// </summary>
    public static bool ResolvePayloadSigning(StorageOptions settings)
    {
        var url = (settings.ServiceUrl ?? string.Empty).Trim();
        var isHttps = url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        if (!isHttps)
            return false;

        if (settings.DisablePayloadSigning is { } explicitChoice)
            return explicitChoice;

        // Auto: only Cloudflare R2 needs it.
        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
               && uri.Host.EndsWith("r2.cloudflarestorage.com", StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public string Name => $"s3:{_bucket}";

    /// <inheritdoc />
    public async Task SaveAsync(string storedName, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        await _client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucket,
            Key = storedName,
            InputStream = content,
            ContentType = contentType,
            // See the constructor: unsigned payloads are an R2 requirement and are only
            // legal over HTTPS, so this is off for a plain-HTTP endpoint.
            DisablePayloadSigning = _disablePayloadSigning,
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Stream?> OpenReadAsync(string storedName, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _client.GetObjectAsync(_bucket, storedName, cancellationToken);
            // Copy to memory so the caller can stream it after the S3 response is disposed.
            var buffer = new MemoryStream();
            await response.ResponseStream.CopyToAsync(buffer, cancellationToken);
            buffer.Position = 0;
            return buffer;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning("File metadata exists but the object is missing in S3. Key: {Key}", storedName);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string storedName, CancellationToken cancellationToken = default)
    {
        // S3 treats deleting a missing key as a success, which is what we want.
        await _client.DeleteObjectAsync(_bucket, storedName, cancellationToken);
    }

    public void Dispose() => _client.Dispose();
}
