using System.Security.Cryptography;

namespace Taskpilot.API.Common;

/// <summary>
/// Minimal RFC 6238 TOTP (time-based one-time password) using the built-in HMAC-SHA1.
/// Secrets are Base32-encoded so they can be entered into standard authenticator apps.
/// </summary>
public static class Totp
{
    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
    private const int StepSeconds = 30;
    private const int Digits = 6;

    /// <summary>Generates a new random Base32 secret (default 20 bytes / 160 bits).</summary>
    public static string GenerateSecret(int bytes = 20)
    {
        var buffer = RandomNumberGenerator.GetBytes(bytes);
        return Base32Encode(buffer);
    }

    /// <summary>The otpauth:// URI an authenticator app scans/imports.</summary>
    public static string BuildUri(string secret, string account, string issuer = "TaskPilot") =>
        $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(account)}" +
        $"?secret={secret}&issuer={Uri.EscapeDataString(issuer)}&digits={Digits}&period={StepSeconds}";

    /// <summary>Validates a code against the secret, allowing ±1 time step for clock drift.</summary>
    public static bool Verify(string secret, string code, DateTimeOffset? now = null)
    {
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(code))
            return false;

        code = code.Trim();
        var counter = (now ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds() / StepSeconds;
        for (var offset = -1; offset <= 1; offset++)
        {
            if (Compute(secret, counter + offset) == code)
                return true;
        }
        return false;
    }

    /// <summary>Computes the code for a given time counter.</summary>
    private static string Compute(string secret, long counter)
    {
        var key = Base32Decode(secret);
        var msg = new byte[8];
        for (var i = 7; i >= 0; i--)
        {
            msg[i] = (byte)(counter & 0xFF);
            counter >>= 8;
        }

        using var hmac = new HMACSHA1(key);
        var hash = hmac.ComputeHash(msg);

        // Dynamic truncation (RFC 4226).
        var o = hash[^1] & 0x0F;
        var binary = ((hash[o] & 0x7F) << 24)
                     | ((hash[o + 1] & 0xFF) << 16)
                     | ((hash[o + 2] & 0xFF) << 8)
                     | (hash[o + 3] & 0xFF);
        var otp = binary % (int)Math.Pow(10, Digits);
        return otp.ToString().PadLeft(Digits, '0');
    }

    private static string Base32Encode(byte[] data)
    {
        var bits = 0;
        var value = 0;
        var sb = new System.Text.StringBuilder();
        foreach (var b in data)
        {
            value = (value << 8) | b;
            bits += 8;
            while (bits >= 5)
            {
                sb.Append(Base32Alphabet[(value >> (bits - 5)) & 31]);
                bits -= 5;
            }
        }
        if (bits > 0)
            sb.Append(Base32Alphabet[(value << (5 - bits)) & 31]);
        return sb.ToString();
    }

    private static byte[] Base32Decode(string input)
    {
        input = input.TrimEnd('=').ToUpperInvariant();
        var bits = 0;
        var value = 0;
        var output = new List<byte>(input.Length * 5 / 8);
        foreach (var c in input)
        {
            var idx = Base32Alphabet.IndexOf(c);
            if (idx < 0) continue;
            value = (value << 5) | idx;
            bits += 5;
            if (bits >= 8)
            {
                output.Add((byte)((value >> (bits - 8)) & 0xFF));
                bits -= 8;
            }
        }
        return output.ToArray();
    }
}
