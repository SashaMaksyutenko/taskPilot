namespace Taskpilot.API.Configuration;

/// <summary>
/// Where uploaded files are stored. Leave the S3 settings empty and files go to the
/// local disk (fine for development). Fill them in and the app uses any S3-compatible
/// bucket — Cloudflare R2, Backblaze B2, MinIO or AWS S3 itself.
///
/// This matters for deployment: hosting platforms hand you an ephemeral filesystem, so
/// files written to disk disappear on the next restart.
/// </summary>
public class StorageOptions
{
    /// <summary>Bucket name.</summary>
    public string Bucket { get; set; } = string.Empty;

    /// <summary>Access key id.</summary>
    public string AccessKey { get; set; } = string.Empty;

    /// <summary>Secret access key.</summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// S3 endpoint. Required for non-AWS providers, e.g.
    /// "https://&lt;account-id&gt;.r2.cloudflarestorage.com" for Cloudflare R2.
    /// Leave empty to talk to AWS S3 in <see cref="Region"/>.
    /// </summary>
    public string ServiceUrl { get; set; } = string.Empty;

    /// <summary>AWS region; ignored when <see cref="ServiceUrl"/> is set. R2 uses "auto".</summary>
    public string Region { get; set; } = "auto";

    /// <summary>True once a bucket and both keys are configured; otherwise the disk is used.</summary>
    public bool S3Configured =>
        !string.IsNullOrWhiteSpace(Bucket)
        && !string.IsNullOrWhiteSpace(AccessKey)
        && !string.IsNullOrWhiteSpace(SecretKey);
}
