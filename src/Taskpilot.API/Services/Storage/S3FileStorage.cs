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
        }
        else
        {
            config.RegionEndpoint = RegionEndpoint.GetBySystemName(settings.Region);
        }

        _client = new AmazonS3Client(credentials, config);
        _logger.LogInformation("File storage: S3 bucket \"{Bucket}\".", _bucket);
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
            // The object is only ever served through our own authenticated endpoints,
            // so the bucket itself stays private.
            DisablePayloadSigning = true,
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
