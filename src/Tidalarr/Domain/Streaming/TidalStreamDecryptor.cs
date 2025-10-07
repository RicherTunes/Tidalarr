using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Tidalarr.Domain.Streaming;

/// <summary>
/// Handles decrypting Tidal audio streams using the shared master key + security token flow.
/// </summary>
public static class TidalStreamDecryptor
{
    private const string MasterKeyBase64 = "UIlTTEMmmLfGowo/UC60x2H45W6MdGgTRfo/umg4754=";
    private static readonly byte[] MasterKey = Convert.FromBase64String(MasterKeyBase64);

    public static byte[] Decrypt(ReadOnlySpan<byte> cipher, string securityToken)
    {
        if (cipher.IsEmpty)
        {
            return Array.Empty<byte>();
        }

        if (string.IsNullOrWhiteSpace(securityToken))
        {
            throw new ArgumentException("Security token is required for decryption.", nameof(securityToken));
        }

        var (key, counterSeed) = DeriveKeyAndCounter(securityToken);
        return DecryptCtr(cipher, key, counterSeed);
    }

    public static MemoryStream DecryptToStream(ReadOnlySpan<byte> cipher, string securityToken)
    {
        var decrypted = Decrypt(cipher, securityToken);
        return new MemoryStream(decrypted, writable: false);
    }

    public static async Task DecryptFileStreamAsync(FileStream stream, string securityToken, CancellationToken cancellationToken = default)
    {
        if (stream is null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        if (string.IsNullOrWhiteSpace(securityToken))
        {
            throw new ArgumentException("Security token is required for decryption.", nameof(securityToken));
        }

        stream.Seek(0, SeekOrigin.Begin);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        var decrypted = Decrypt(buffer.ToArray(), securityToken);

        stream.Seek(0, SeekOrigin.Begin);
        stream.SetLength(0);
        await stream.WriteAsync(decrypted, 0, decrypted.Length, cancellationToken).ConfigureAwait(false);
        stream.Seek(0, SeekOrigin.Begin);
    }

    private static (byte[] Key, byte[] CounterSeed) DeriveKeyAndCounter(string securityToken)
    {
        var tokenBytes = Convert.FromBase64String(securityToken);
        if (tokenBytes.Length < 24)
        {
            throw new InvalidOperationException("Security token is malformed.");
        }

        var iv = new byte[16];
        Buffer.BlockCopy(tokenBytes, 0, iv, 0, iv.Length);
        var encrypted = new byte[tokenBytes.Length - iv.Length];
        Buffer.BlockCopy(tokenBytes, iv.Length, encrypted, 0, encrypted.Length);

        using var aes = Aes.Create();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.None;
        aes.Key = MasterKey;
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        var decrypted = decryptor.TransformFinalBlock(encrypted, 0, encrypted.Length);
        if (decrypted.Length < 24)
        {
            throw new InvalidOperationException("Security token payload is incomplete.");
        }

        var key = new byte[16];
        Buffer.BlockCopy(decrypted, 0, key, 0, key.Length);

        var counter = new byte[16];
        if (decrypted.Length >= 32)
        {
            Buffer.BlockCopy(decrypted, 16, counter, 0, counter.Length);
        }
        else
        {
            // Older payloads may provide only 8 bytes for the counter seed — preserve compatibility.
            Buffer.BlockCopy(decrypted, 16, counter, 0, 8);
        }

        return (key, counter);
    }

    private static byte[] DecryptCtr(ReadOnlySpan<byte> cipher, byte[] key, byte[] counterSeed)
    {
        using var aes = Aes.Create();
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;
        aes.Key = key;

        using var encryptor = aes.CreateEncryptor();
        var counter = new byte[16];
        Buffer.BlockCopy(counterSeed, 0, counter, 0, Math.Min(counterSeed.Length, counter.Length));

        var output = new byte[cipher.Length];
        var keystream = new byte[16];

        var offset = 0;
        while (offset < cipher.Length)
        {
            encryptor.TransformBlock(counter, 0, counter.Length, keystream, 0);
            var blockSize = Math.Min(keystream.Length, cipher.Length - offset);
            for (var i = 0; i < blockSize; i++)
            {
                output[offset + i] = (byte)(cipher[offset + i] ^ keystream[i]);
            }

            offset += blockSize;
            IncrementCounter(counter);
        }

        return output;
    }

    private static void IncrementCounter(byte[] counter)
    {
        for (var i = counter.Length - 1; i >= 0; i--)
        {
            if (++counter[i] != 0)
            {
                break;
            }
        }
    }
}


