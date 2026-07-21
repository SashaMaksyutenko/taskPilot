using Taskpilot.API.Configuration;
using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>
/// Tests for the payload-signing decision in <see cref="S3FileStorage"/>. Getting this wrong
/// breaks every upload for a whole provider, and the failure only shows up at runtime:
/// Cloudflare R2 rejects the SDK's chunked signature, while Supabase, Backblaze B2, MinIO and
/// AWS all expect it.
/// </summary>
public class S3PayloadSigningTests
{
    private static StorageOptions Options(string? serviceUrl, bool? explicitChoice = null) => new()
    {
        Bucket = "files",
        AccessKey = "key",
        SecretKey = "secret",
        ServiceUrl = serviceUrl ?? string.Empty,
        DisablePayloadSigning = explicitChoice,
    };

    [Fact]
    public void CloudflareR2_SkipsSigning()
    {
        var settings = Options("https://abc123.r2.cloudflarestorage.com");

        Assert.True(S3FileStorage.ResolvePayloadSigning(settings));
    }

    [Theory]
    [InlineData("https://myproject.storage.supabase.co/storage/v1/s3")]  // Supabase
    [InlineData("https://s3.eu-central-003.backblazeb2.com")]            // Backblaze B2
    [InlineData("https://minio.example.com")]                            // self-hosted
    public void EveryOtherProvider_KeepsSigning(string serviceUrl)
    {
        Assert.False(S3FileStorage.ResolvePayloadSigning(Options(serviceUrl)));
    }

    [Fact]
    public void PlainHttp_NeverSkipsSigning_BecauseTheSdkForbidsIt()
    {
        // Even a host that would otherwise qualify: the SDK throws when the two combine.
        Assert.False(S3FileStorage.ResolvePayloadSigning(Options("http://abc123.r2.cloudflarestorage.com")));
        Assert.False(S3FileStorage.ResolvePayloadSigning(Options("http://localhost:9000")));
    }

    [Fact]
    public void AmazonS3_WithNoCustomEndpoint_KeepsSigning()
    {
        Assert.False(S3FileStorage.ResolvePayloadSigning(Options(serviceUrl: null)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AnExplicitSetting_OverridesTheGuess(bool choice)
    {
        // A provider that disagrees with the host-based guess can be corrected by config.
        var supabase = Options("https://myproject.storage.supabase.co/storage/v1/s3", explicitChoice: choice);
        Assert.Equal(choice, S3FileStorage.ResolvePayloadSigning(supabase));
    }

    [Fact]
    public void AnExplicitSetting_CannotForceItOverPlainHttp()
    {
        // Config must not be able to create the combination the SDK rejects.
        var settings = Options("http://minio.local:9000", explicitChoice: true);

        Assert.False(S3FileStorage.ResolvePayloadSigning(settings));
    }
}
