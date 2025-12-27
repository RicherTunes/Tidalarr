using System.Security.Cryptography;
using System.Net;
using System.Text;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Streaming;

namespace Tidalarr.Tests;

public class TidalChunkDownloaderTests
{
    private static readonly byte[] TestKey = [.. Enumerable.Range(0, 16).Select(i => (byte)i)];
    private static readonly byte[] TestCounter = [.. Enumerable.Range(16, 8).Select(i => (byte)i)];
    private static readonly byte[] TokenIv = [.. Enumerable.Range(24, 16).Select(i => (byte)i)];

    [Fact]
    public void ChunkDelayMs_DefaultValue_Is50()
    {
        // Arrange & Act
        HttpClient httpClient = new();
        TidalChunkDownloader downloader = new(httpClient);

        // Assert - default delay is 50ms
        Assert.Equal(50, downloader.ChunkDelayMs);
    }

    [Fact]
    public void ChunkDelayMs_SetToZero_DisablesDelay()
    {
        // Arrange & Act
        HttpClient httpClient = new();
        TidalChunkDownloader downloader = new(httpClient, chunkDelayMs: 0);

        // Assert - 0 should be stored (disables delay)
        Assert.Equal(0, downloader.ChunkDelayMs);
    }

    [Fact]
    public void ChunkDelayMs_CustomValue_IsRespected()
    {
        // Arrange & Act
        HttpClient httpClient = new();
        TidalChunkDownloader downloader = new(httpClient, chunkDelayMs: 100);

        // Assert
        Assert.Equal(100, downloader.ChunkDelayMs);
    }

    [Fact]
    public async Task DownloadAndAssemble_ValidUrls_ReturnsAssembledStream()
    {
        // Arrange
        string[] testData = ["chunk1data", "chunk2data", "chunk3data"];
        HttpClient httpClient = CreateMockHttpClientWithChunks(testData);
        TidalChunkDownloader downloader = new(httpClient);

        TidalStreamInfo streamInfo = new(
            TrackId: "123",
            ChunkUrls: ["https://test.com/1", "https://test.com/2", "https://test.com/3"],
            FileExtension: ".flac",
            MimeType: "application/dash+xml",
            IsEncrypted: false,
            SecurityToken: null
        );

        // Act
        using Stream result = await downloader.DownloadAndAssembleAsync(streamInfo);

        // Assert
        Assert.NotNull(result);
        using StreamReader reader = new(result, Encoding.UTF8, leaveOpen: true);
        _ = result.Seek(0, SeekOrigin.Begin);
        string content = reader.ReadToEnd();
        Assert.Equal("chunk1datachunk2datachunk3data", content);
    }

    [Fact]
    public async Task ValidateChunkAccessibility_EmptyUrls_ReturnsFalse()
    {
        // Arrange
        HttpClient httpClient = new();
        TidalChunkDownloader downloader = new(httpClient);

        // Act
        bool result = await downloader.ValidateChunkAccessibilityAsync([]);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task DownloadAndAssembleAsync_EncryptedManifest_DecryptsChunkData()
    {
        byte[] plain = Encoding.UTF8.GetBytes("chunk-plain-data");
        (string token, byte[][] encryptedChunks) = BuildEncryptedChunks([plain]);

        HttpClient httpClient = CreateMockHttpClientWithBinaryChunks(encryptedChunks);
        TidalChunkDownloader downloader = new(httpClient);

        TidalManifest manifest = new(
            ChunkUrls: ["https://test.com/1"],
            Codec: "flac",
            MimeType: "application/dash+xml",
            FileExtension: ".m4a",
            SampleRate: 44100,
            IsEncrypted: true,
            KeyId: "kid-1",
            SecurityToken: token);

        using MemoryStream result = await downloader.DownloadAndAssembleAsync(manifest);
        using MemoryStream ms = new();
        await result.CopyToAsync(ms);
        Assert.Equal(plain, ms.ToArray());
    }

    [Fact]
    public async Task DownloadAndAssembleAsync_EncryptedManifestMissingToken_Throws()
    {
        HttpClient httpClient = CreateMockHttpClientWithBinaryChunks([[0x01, 0x02]]);
        TidalChunkDownloader downloader = new(httpClient);
        TidalManifest manifest = new(
            ChunkUrls: ["https://test.com/1"],
            Codec: "flac",
            MimeType: "application/dash+xml",
            FileExtension: ".m4a",
            SampleRate: 44100,
            IsEncrypted: true,
            KeyId: "kid-1",
            SecurityToken: null);

        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => downloader.DownloadAndAssembleAsync(manifest));
    }

    private static (string Token, byte[][] Chunks) BuildEncryptedChunks(byte[][] plainChunks)
    {
        string token = BuildSecurityToken(TestKey, TestCounter, TokenIv);
        byte[] concatenated = [.. plainChunks.SelectMany(b => b)];
        byte[] encryptedAll = EncryptCtr(concatenated, TestKey, TestCounter);

        byte[][] encryptedChunks = new byte[plainChunks.Length][];
        int offset = 0;
        for (int i = 0; i < plainChunks.Length; i++)
        {
            int length = plainChunks[i].Length;
            encryptedChunks[i] = new byte[length];
            Array.Copy(encryptedAll, offset, encryptedChunks[i], 0, length);
            offset += length;
        }

        return (token, encryptedChunks);
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

    private static HttpClient CreateMockHttpClientWithChunks(string[] chunks)
    {
        byte[][] bytes = [.. chunks.Select(Encoding.UTF8.GetBytes)];
        MockChunkHttpMessageHandler handler = new(bytes);
        return new HttpClient(handler);
    }

    private static HttpClient CreateMockHttpClientWithBinaryChunks(byte[][] chunks)
    {
        MockChunkHttpMessageHandler handler = new(chunks);
        return new HttpClient(handler);
    }
}

public class MockChunkHttpMessageHandler(byte[][] chunks) : HttpMessageHandler
{
    private readonly byte[][] _chunks = chunks;
    private int _chunkIndex = 0;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        HttpResponseMessage response = new(HttpStatusCode.OK);
        // Return chunks in order
        if (this._chunkIndex < this._chunks.Length)
        {
            response.Content = new ByteArrayContent(this._chunks[this._chunkIndex]);
            this._chunkIndex++;
        }
        else
        {
            response.Content = new ByteArrayContent([]);
        }

        return Task.FromResult(response);
    }
}




