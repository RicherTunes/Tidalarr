using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Tidalarr.Domain.Streaming;
using Xunit;

namespace Tidalarr.Tests.Unit;

public class TidalStreamDecryptorTests
{
    private static readonly byte[] TestKey = Enumerable.Range(0, 16).Select(i => (byte)i).ToArray();
    private static readonly byte[] Counter8 = Enumerable.Range(16, 8).Select(i => (byte)i).ToArray();
    private static readonly byte[] Counter16 = Enumerable.Range(16, 16).Select(i => (byte)i).ToArray();
    private static readonly byte[] TokenIv = Enumerable.Range(32, 16).Select(i => (byte)i).ToArray();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Decrypt_ReturnsPlaintext_ForKnownCipher(bool useFullCounter)
    {
        var counter = useFullCounter ? Counter16 : Counter8;
        var plain = Enumerable.Range(100, 48).Select(i => (byte)i).ToArray();
        var cipher = EncryptCtr(plain, TestKey, counter);
        var securityToken = BuildSecurityToken(TestKey, counter, TokenIv);

        var actual = TidalStreamDecryptor.Decrypt(cipher, securityToken);

        Assert.Equal(plain, actual);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DecryptFileStreamAsync_RewritesStream(bool useFullCounter)
    {
        var counter = useFullCounter ? Counter16 : Counter8;
        var plain = Enumerable.Range(60, 32).Select(i => (byte)i).ToArray();
        var cipher = EncryptCtr(plain, TestKey, counter);
        var securityToken = BuildSecurityToken(TestKey, counter, TokenIv);

        using var temp = new FileStream(Path.GetTempFileName(), FileMode.Create, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.DeleteOnClose);
        await temp.WriteAsync(cipher);
        await temp.FlushAsync();

        await TidalStreamDecryptor.DecryptFileStreamAsync(temp, securityToken);

        temp.Seek(0, SeekOrigin.Begin);
        var buffer = new byte[plain.Length];
        var read = await temp.ReadAsync(buffer);

        Assert.Equal(plain.Length, read);
        Assert.Equal(plain, buffer);
    }

    [Fact]
    public void Decrypt_ThrowsWhenTokenMissing()
    {
        Assert.Throws<ArgumentException>(() => TidalStreamDecryptor.Decrypt(new byte[] { 0x01 }, string.Empty));
    }

    private static string BuildSecurityToken(byte[] key, byte[] counter, byte[] iv)
    {
        var payload = new byte[key.Length + Math.Max(counter.Length, 16)];
        Buffer.BlockCopy(key, 0, payload, 0, key.Length);
        Buffer.BlockCopy(counter, 0, payload, key.Length, counter.Length);

        using var aes = Aes.Create();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.None;
        aes.Key = Convert.FromBase64String("UIlTTEMmmLfGowo/UC60x2H45W6MdGgTRfo/umg4754=");
        aes.IV = iv;

        using var encryptor = aes.CreateEncryptor();
        var encrypted = encryptor.TransformFinalBlock(payload, 0, payload.Length);

        var tokenBytes = new byte[iv.Length + encrypted.Length];
        Buffer.BlockCopy(iv, 0, tokenBytes, 0, iv.Length);
        Buffer.BlockCopy(encrypted, 0, tokenBytes, iv.Length, encrypted.Length);
        return Convert.ToBase64String(tokenBytes);
    }

    private static byte[] EncryptCtr(byte[] plain, byte[] key, byte[] counterSeed)
    {
        using var aes = Aes.Create();
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;
        aes.Key = key;

        using var encryptor = aes.CreateEncryptor();
        var counter = new byte[16];
        Buffer.BlockCopy(counterSeed, 0, counter, 0, Math.Min(counterSeed.Length, counter.Length));

        var output = new byte[plain.Length];
        var keystream = new byte[16];
        var offset = 0;
        while (offset < plain.Length)
        {
            encryptor.TransformBlock(counter, 0, counter.Length, keystream, 0);
            var blockSize = Math.Min(keystream.Length, plain.Length - offset);
            for (var i = 0; i < blockSize; i++)
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
        for (var i = counter.Length - 1; i >= 0; i--)
        {
            if (++counter[i] != 0)
            {
                break;
            }
        }
    }
}

