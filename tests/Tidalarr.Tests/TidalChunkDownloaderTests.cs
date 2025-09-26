using System.IO;
using System.Security.Cryptography;
using System.Linq;
using System.Net;
using System.Text;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Streaming;
using Xunit;

namespace Tidalarr.Tests;

public class TidalChunkDownloaderTests
{
    private static readonly byte[] TestKey = Enumerable.Range(0, 16).Select(i => (byte)i).ToArray();
    private static readonly byte[] TestCounter = Enumerable.Range(16, 8).Select(i => (byte)i).ToArray();
    private static readonly byte[] TokenIv = Enumerable.Range(24, 16).Select(i => (byte)i).ToArray();

    [Fact]
    public async Task DownloadAndAssemble_ValidUrls_ReturnsAssembledStream()
    {
        // Arrange
        var testData = new[] { "chunk1data", "chunk2data", "chunk3data" };
        var httpClient = CreateMockHttpClientWithChunks(testData);
        var downloader = new TidalChunkDownloader(httpClient);

        var streamInfo = new TidalStreamInfo(
            TrackId: "123",
            ChunkUrls: new[] { "https://test.com/1", "https://test.com/2", "https://test.com/3" },
            FileExtension: ".flac",
            MimeType: "application/dash+xml",
            IsEncrypted: false,
            SecurityToken: null
        );

        // Act
        using var result = await downloader.DownloadAndAssembleAsync(streamInfo);

        // Assert
        Assert.NotNull(result);
        using var reader = new StreamReader(result, Encoding.UTF8, leaveOpen: true);
        result.Seek(0, SeekOrigin.Begin);
        var content = reader.ReadToEnd();
        Assert.Equal("chunk1datachunk2datachunk3data", content);
    }

    [Fact]
    public async Task ValidateChunkAccessibility_EmptyUrls_ReturnsFalse()
    {
        // Arrange
        var httpClient = new HttpClient();
        var downloader = new TidalChunkDownloader(httpClient);

        // Act
        var result = await downloader.ValidateChunkAccessibilityAsync(Array.Empty<string>());

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task DownloadAndAssembleAsync_EncryptedManifest_DecryptsChunkData()
    {
        var plain = Encoding.UTF8.GetBytes("chunk-plain-data");
        var (token, encryptedChunks) = BuildEncryptedChunks(new[] { plain });

        var httpClient = CreateMockHttpClientWithBinaryChunks(encryptedChunks);
        var downloader = new TidalChunkDownloader(httpClient);

        var manifest = new TidalManifest(
            ChunkUrls: new[] { "https://test.com/1" },
            Codec: "flac",
            MimeType: "application/dash+xml",
            FileExtension: ".m4a",
            SampleRate: 44100,
            IsEncrypted: true,
            KeyId: "kid-1",
            SecurityToken: token);

        using var result = await downloader.DownloadAndAssembleAsync(manifest);
        using var ms = new MemoryStream();
        await result.CopyToAsync(ms);
        Assert.Equal(plain, ms.ToArray());
    }

    [Fact]
    public async Task DownloadAndAssembleAsync_EncryptedManifestMissingToken_Throws()
    {
        var httpClient = CreateMockHttpClientWithBinaryChunks(new[] { new byte[] { 0x01, 0x02 } });
        var downloader = new TidalChunkDownloader(httpClient);
        var manifest = new TidalManifest(
            ChunkUrls: new[] { "https://test.com/1" },
            Codec: "flac",
            MimeType: "application/dash+xml",
            FileExtension: ".m4a",
            SampleRate: 44100,
            IsEncrypted: true,
            KeyId: "kid-1",
            SecurityToken: null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => downloader.DownloadAndAssembleAsync(manifest));
    }

    private static (string Token, byte[][] Chunks) BuildEncryptedChunks(byte[][] plainChunks)
    {
        var token = BuildSecurityToken(TestKey, TestCounter, TokenIv);
        var concatenated = plainChunks.SelectMany(b => b).ToArray();
        var encryptedAll = EncryptCtr(concatenated, TestKey, TestCounter);

        var encryptedChunks = new byte[plainChunks.Length][];
        var offset = 0;
        for (var i = 0; i < plainChunks.Length; i++)
        {
            var length = plainChunks[i].Length;
            encryptedChunks[i] = new byte[length];
            Array.Copy(encryptedAll, offset, encryptedChunks[i], 0, length);
            offset += length;
        }

        return (token, encryptedChunks);
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

    private static HttpClient CreateMockHttpClientWithChunks(string[] chunks)
    {
        var bytes = chunks.Select(c => Encoding.UTF8.GetBytes(c)).ToArray();
        var handler = new MockChunkHttpMessageHandler(bytes);
        return new HttpClient(handler);
    }

    private static HttpClient CreateMockHttpClientWithBinaryChunks(byte[][] chunks)
    {
        var handler = new MockChunkHttpMessageHandler(chunks);
        return new HttpClient(handler);
    }
}

public class MockChunkHttpMessageHandler : HttpMessageHandler
{
    private readonly byte[][] _chunks;
    private int _chunkIndex = 0;

    public MockChunkHttpMessageHandler(byte[][] chunks)
    {
        _chunks = chunks;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        // Return chunks in order
        if (_chunkIndex < _chunks.Length)
        {
            response.Content = new ByteArrayContent(_chunks[_chunkIndex]);
            _chunkIndex++;
        }
        else
        {
            response.Content = new ByteArrayContent(Array.Empty<byte>());
        }

        return Task.FromResult(response);
    }
}



