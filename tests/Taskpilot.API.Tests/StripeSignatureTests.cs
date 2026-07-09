using System.Security.Cryptography;
using System.Text;
using Taskpilot.API.Common;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>Unit tests for Stripe webhook signature verification (HMAC-SHA256).</summary>
public class StripeSignatureTests
{
    private const string Secret = "whsec_test_secret";

    private static string Sign(string payload, string secret, long ts)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes($"{ts}.{payload}"));
        var sig = Convert.ToHexString(hash).ToLowerInvariant();
        return $"t={ts},v1={sig}";
    }

    [Fact]
    public void Verify_WithValidSignature_ReturnsTrue()
    {
        var now = DateTimeOffset.UtcNow;
        var payload = "{\"type\":\"checkout.session.completed\"}";
        var header = Sign(payload, Secret, now.ToUnixTimeSeconds());

        Assert.True(StripeSignature.Verify(payload, header, Secret, now));
    }

    [Fact]
    public void Verify_WithWrongSecret_ReturnsFalse()
    {
        var now = DateTimeOffset.UtcNow;
        var payload = "{\"x\":1}";
        var header = Sign(payload, "whsec_other", now.ToUnixTimeSeconds());

        Assert.False(StripeSignature.Verify(payload, header, Secret, now));
    }

    [Fact]
    public void Verify_WithTamperedPayload_ReturnsFalse()
    {
        var now = DateTimeOffset.UtcNow;
        var header = Sign("{\"amount\":10}", Secret, now.ToUnixTimeSeconds());

        Assert.False(StripeSignature.Verify("{\"amount\":9999}", header, Secret, now));
    }

    [Fact]
    public void Verify_WithStaleTimestamp_ReturnsFalse()
    {
        var now = DateTimeOffset.UtcNow;
        var payload = "{\"x\":1}";
        // Signed 10 minutes ago — outside the 5-minute tolerance.
        var header = Sign(payload, Secret, now.AddMinutes(-10).ToUnixTimeSeconds());

        Assert.False(StripeSignature.Verify(payload, header, Secret, now));
    }

    [Fact]
    public void Verify_WithMissingHeaderOrSecret_ReturnsFalse()
    {
        Assert.False(StripeSignature.Verify("{}", null, Secret));
        Assert.False(StripeSignature.Verify("{}", "t=1,v1=abc", ""));
    }
}
