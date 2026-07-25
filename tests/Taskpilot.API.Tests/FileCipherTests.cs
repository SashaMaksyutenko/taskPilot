using System.Security.Cryptography;
using System.Text;
using Taskpilot.API.Common;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>
/// Tests the AES-256-GCM file cipher (spec module 18: file encryption at rest): round-trips,
/// authenticates (a wrong key or tampered bytes fail), tells encrypted from plaintext, and
/// validates the configured key.
/// </summary>
public class FileCipherTests
{
    private static byte[] NewKey() => RandomNumberGenerator.GetBytes(32);

    [Fact]
    public void Encrypt_ThenDecrypt_RoundTripsTheContent()
    {
        var key = NewKey();
        var plaintext = Encoding.UTF8.GetBytes("The quick brown fox 🦊 jumps over the lazy dog.");

        var blob = FileCipher.Encrypt(key, plaintext);
        var back = FileCipher.Decrypt(key, blob);

        Assert.Equal(plaintext, back);
        // The stored blob is not the plaintext.
        Assert.NotEqual(plaintext, blob.Take(plaintext.Length).ToArray());
    }

    [Fact]
    public void LooksEncrypted_TrueForCipherBlob_FalseForPlaintext()
    {
        var key = NewKey();
        var blob = FileCipher.Encrypt(key, Encoding.UTF8.GetBytes("secret"));

        Assert.True(FileCipher.LooksEncrypted(blob));
        Assert.False(FileCipher.LooksEncrypted(Encoding.UTF8.GetBytes("just a normal file")));
        Assert.False(FileCipher.LooksEncrypted(Array.Empty<byte>()));
    }

    [Fact]
    public void Decrypt_WithTheWrongKey_Throws()
    {
        var blob = FileCipher.Encrypt(NewKey(), Encoding.UTF8.GetBytes("secret"));

        Assert.Throws<AuthenticationTagMismatchException>(() => FileCipher.Decrypt(NewKey(), blob));
    }

    [Fact]
    public void Decrypt_TamperedCiphertext_Throws()
    {
        var key = NewKey();
        var blob = FileCipher.Encrypt(key, Encoding.UTF8.GetBytes("secret"));
        blob[^1] ^= 0xFF; // flip a bit in the ciphertext

        Assert.Throws<AuthenticationTagMismatchException>(() => FileCipher.Decrypt(key, blob));
    }

    [Fact]
    public void Encrypt_UsesAFreshNonce_SoTwoEncryptionsDiffer()
    {
        var key = NewKey();
        var plaintext = Encoding.UTF8.GetBytes("same input");

        var a = FileCipher.Encrypt(key, plaintext);
        var b = FileCipher.Encrypt(key, plaintext);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void ParseKey_AcceptsA32ByteBase64Key_RejectsOthers()
    {
        var valid = Convert.ToBase64String(NewKey());
        Assert.Equal(32, FileCipher.ParseKey(valid).Length);

        Assert.Throws<InvalidOperationException>(() => FileCipher.ParseKey("not base64!!"));
        Assert.Throws<InvalidOperationException>(() => FileCipher.ParseKey(Convert.ToBase64String(new byte[16])));
    }
}
