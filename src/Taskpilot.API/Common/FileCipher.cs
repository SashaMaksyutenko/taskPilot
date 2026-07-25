using System.Security.Cryptography;

namespace Taskpilot.API.Common;

/// <summary>
/// Authenticated encryption of file bytes at rest, using AES-256-GCM. Each blob is
/// self-describing: it starts with a short magic marker so encrypted and legacy-plaintext
/// files can be told apart on read without any database flag.
///
/// Stored layout: <c>MAGIC (6) || nonce (12) || tag (16) || ciphertext</c>.
/// GCM authenticates the ciphertext, so tampering or a wrong key fails loudly on decrypt.
/// </summary>
public static class FileCipher
{
    // "TPENC1" — a collision with a real plaintext file's first 6 bytes is ~1 in 2.8e14.
    private static readonly byte[] Magic = "TPENC1"u8.ToArray();
    private const int NonceSize = 12; // AES-GCM standard nonce
    private const int TagSize = 16;   // AES-GCM authentication tag
    private const int HeaderSize = 6 + NonceSize + TagSize;

    /// <summary>True when the blob carries this cipher's marker (so it should be decrypted).</summary>
    public static bool LooksEncrypted(ReadOnlySpan<byte> blob) =>
        blob.Length >= HeaderSize && blob[..Magic.Length].SequenceEqual(Magic);

    /// <summary>Encrypts <paramref name="plaintext"/> into a self-describing blob.</summary>
    public static byte[] Encrypt(byte[] key, byte[] plaintext)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        var blob = new byte[HeaderSize + ciphertext.Length];
        Magic.CopyTo(blob, 0);
        nonce.CopyTo(blob, Magic.Length);
        tag.CopyTo(blob, Magic.Length + NonceSize);
        ciphertext.CopyTo(blob, HeaderSize);
        return blob;
    }

    /// <summary>
    /// Decrypts a blob produced by <see cref="Encrypt"/>. Throws
    /// <see cref="CryptographicException"/> if the key is wrong or the bytes were tampered with.
    /// </summary>
    public static byte[] Decrypt(byte[] key, byte[] blob)
    {
        if (!LooksEncrypted(blob))
            throw new CryptographicException("Blob is not in the expected encrypted format.");

        var nonce = blob.AsSpan(Magic.Length, NonceSize);
        var tag = blob.AsSpan(Magic.Length + NonceSize, TagSize);
        var ciphertext = blob.AsSpan(HeaderSize);
        var plaintext = new byte[ciphertext.Length];

        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);
        return plaintext;
    }

    /// <summary>
    /// Parses a base64 AES-256 key, validating it decodes to exactly 32 bytes. Throws
    /// <see cref="InvalidOperationException"/> with a clear message otherwise (fail fast at startup).
    /// </summary>
    public static byte[] ParseKey(string base64Key)
    {
        byte[] key;
        try
        {
            key = Convert.FromBase64String(base64Key);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException("Storage:EncryptionKey must be valid base64.");
        }

        if (key.Length != 32)
            throw new InvalidOperationException(
                $"Storage:EncryptionKey must decode to 32 bytes for AES-256; got {key.Length}.");
        return key;
    }
}
