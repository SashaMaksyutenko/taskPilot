using System.Security.Cryptography;
using System.Text;

namespace Taskpilot.API.Common;

/// <summary>
/// Verifies the "Stripe-Signature" header on incoming webhooks (HMAC-SHA256 over
/// "{timestamp}.{payload}" with the endpoint signing secret), matching Stripe's
/// scheme without the Stripe SDK. Also rejects stale timestamps to block replays.
/// </summary>
public static class StripeSignature
{
    private const int ToleranceSeconds = 300; // 5 minutes

    /// <summary>Returns true when the payload matches the signature header for the given secret.</summary>
    public static bool Verify(string payload, string? signatureHeader, string secret, DateTimeOffset? now = null)
    {
        if (string.IsNullOrWhiteSpace(signatureHeader) || string.IsNullOrWhiteSpace(secret))
            return false;

        // Header looks like: t=1699999999,v1=abc...,v1=def...
        string? timestamp = null;
        var signatures = new List<string>();
        foreach (var part in signatureHeader.Split(','))
        {
            var kv = part.Split('=', 2);
            if (kv.Length != 2) continue;
            if (kv[0] == "t") timestamp = kv[1];
            else if (kv[0] == "v1") signatures.Add(kv[1]);
        }

        if (timestamp is null || signatures.Count == 0 || !long.TryParse(timestamp, out var ts))
            return false;

        // Reject timestamps outside the tolerance window (replay protection).
        var eventTime = DateTimeOffset.FromUnixTimeSeconds(ts);
        var current = now ?? DateTimeOffset.UtcNow;
        if (Math.Abs((current - eventTime).TotalSeconds) > ToleranceSeconds)
            return false;

        var signedPayload = $"{timestamp}.{payload}";
        var expected = ComputeHmac(signedPayload, secret);

        // Constant-time compare against each provided v1 signature.
        foreach (var sig in signatures)
        {
            if (FixedTimeEquals(expected, sig))
                return true;
        }
        return false;
    }

    private static string ComputeHmac(string data, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        var ba = Encoding.UTF8.GetBytes(a);
        var bb = Encoding.UTF8.GetBytes(b);
        return ba.Length == bb.Length && CryptographicOperations.FixedTimeEquals(ba, bb);
    }
}
