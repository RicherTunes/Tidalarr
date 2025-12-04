using System.Security.Cryptography;
using Tidalarr.Domain.Streaming;

namespace Tidalarr.Tests.Unit;

public class TidalStreamDecryptorTests
{
    private static readonly byte[] TestKey = [.. Enumerable.Range(0, 16).Select(i => (byte)i)];
    private static readonly byte[] Counter8 = [.. Enumerable.Range(16, 8).Select(i => (byte)i)];
    private static readonly byte[] Counter16 = [.. Enumerable.Range(16, 16).Select(i => (byte)i)];
    private static readonly byte[] TokenIv = [.. Enumerable.Range(32, 16).Select(i => (byte)i)];

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Decrypt_ReturnsPlaintext_ForKnownCipher(bool useFullCounter)
    {
        byte[] counter = useFullCounter ? Counter16 : Counter8;
        byte[] plain = [.. Enumerable.Range(100, 48).Select(i => (byte)i)];
        byte[] cipher = EncryptCtr(plain, TestKey, counter);
        string securityToken = BuildSecurityToken(TestKey, counter, TokenIv);

        byte[] actual = TidalStreamDecryptor.Decrypt(cipher, securityToken);

        Assert.Equal(plain, actual);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DecryptFileStreamAsync_RewritesStream(bool useFullCounter)
    {
        byte[] counter = useFullCounter ? Counter16 : Counter8;
        byte[] plain = [.. Enumerable.Range(60, 32).Select(i => (byte)i)];
        byte[] cipher = EncryptCtr(plain, TestKey, counter);
        string securityToken = BuildSecurityToken(TestKey, counter, TokenIv);

        using FileStream temp = new(Path.GetTempFileName(), FileMode.Create, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.DeleteOnClose);
        await temp.WriteAsync(cipher);
        await temp.FlushAsync();

        await TidalStreamDecryptor.DecryptFileStreamAsync(temp, securityToken);

        _ = temp.Seek(0, SeekOrigin.Begin);
        byte[] buffer = new byte[plain.Length];
        int read = await temp.ReadAsync(buffer);

        Assert.Equal(plain.Length, read);
        Assert.Equal(plain, buffer);
    }

    [Fact]
    public void Decrypt_ThrowsWhenTokenMissing()
    {
        _ = Assert.Throws<ArgumentException>(() => TidalStreamDecryptor.Decrypt([0x01], string.Empty));
    }

    private static string BuildSecurityToken(byte[] key, byte[] counter, byte[] iv)
    {
        byte[] payload = new byte[key.Length + Math.Max(counter.Length, 16)];
        Buffer.BlockCopy(key, 0, payload, 0, key.Length);
        Buffer.BlockCopy(counter, 0, payload, key.Length, counter.Length);

        using Aes aes = Aes.Create();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.None;
        aes.Key = Convert.FromBase64String("UIlTTEMmmLfGowo/UC60x2H45W6MdGgTRfo/umg4754=");
        aes.IV = iv;

        using ICryptoTransform encryptor = aes.CreateEncryptor();
        byte[] encrypted = encryptor.TransformFinalBlock(payload, 0, payload.Length);

        byte[] tokenBytes = new byte[iv.Length + encrypted.Length];
        Buffer.BlockCopy(iv, 0, tokenBytes, 0, iv.Length);
        Buffer.BlockCopy(encrypted, 0, tokenBytes, iv.Length, encrypted.Length);
        return Convert.ToBase64String(tokenBytes);
    }

    private static byte[] EncryptCtr(byte[] plain, byte[] key, byte[] counterSeed)
    {
        using Aes aes = Aes.Create();
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;
        aes.Key = key;

        using ICryptoTransform encryptor = aes.CreateEncryptor();
        byte[] counter = new byte[16];
        Buffer.BlockCopy(counterSeed, 0, counter, 0, Math.Min(counterSeed.Length, counter.Length));

        byte[] output = new byte[plain.Length];
        byte[] keystream = new byte[16];
        int offset = 0;
        while (offset < plain.Length)
        {
            _ = encryptor.TransformBlock(counter, 0, counter.Length, keystream, 0);
            int blockSize = Math.Min(keystream.Length, plain.Length - offset);
            for (int i = 0; i < blockSize; i++)
            {
                output[offset + i] = (byte)(plain[offset + i] ^ keystream[i]);
            }

            offset += blockSize;
            IncrementCounter(counter);
        }

        return output;
    }

    private static void IncrementCounter(byte[] counter)
    {
        for (int i = counter.Length - 1; i >= 0; i--)
        {
            if (++counter[i] != 0)
            {
                break;
            }
        }
    }
}




