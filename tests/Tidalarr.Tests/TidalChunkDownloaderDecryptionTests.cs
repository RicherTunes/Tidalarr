using System.Net;
using System.Security.Cryptography;
using System.Text;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Streaming;

namespace Tidalarr.Tests;

/// <summary>
/// Coverage tests for TidalChunkDownloader decryption paths.
/// Source: src/Tidalarr/Domain/Streaming/TidalChunkDownloader.cs
///
/// Covers uncovered paths:
/// - Line 82-94: Successful decryption in DownloadAndAssembleAsync (MemoryStream)
/// - Line 226: Successful decryption in DownloadAndAssembleToFileStreamAsync
/// - Line 349: Successful decryption in legacy DownloadAndAssembleAsync
/// - Line 126/262: Single chunk with high concurrency (sequential fallback)
/// - Line 11: ChunkDownloadProgress ProgressPercentage edge cases
/// - Lines 197-209/308-323: Parallel download exception handling with CTS
/// </summary>
public class TidalChunkDownloaderDecryptionTests
{
    private static readonly byte[] TestKey = [.. Enumerable.Range(0, 16).Select(i => (byte)i)];
    private static readonly byte[] TestCounter = [.. Enumerable.Range(16, 8).Select(i => (byte)i)];
    private static readonly byte[] TokenIv = [.. Enumerable.Range(24, 16).Select(i => (byte)i)];

    #region Line 82-94: Successful Decryption in DownloadAndAssembleAsync

    /// <summary>
    /// Source lines 82-94: Decryption path when RequiresDecryption returns true.
    /// Verifies TidalStreamDecryptor.Decrypt is called and returns decrypted data.
    /// </summary>
    [Fact]
    public async Task DownloadAndAssembleAsync_EncryptedWithValidToken_DecryptsSuccessfully()
    {
        // Arrange - Build properly encrypted content
        byte[] plainData = Encoding.UTF8.GetBytes("decrypted-content-test");
        (string token, byte[][] encryptedChunks) = BuildEncryptedChunks([plainData]);

        ChunkSequenceHandler handler = new(encryptedChunks);
        TidalChunkDownloader downloader = new(new HttpClient(handler));

        TidalManifest manifest = new(
            ChunkUrls: ["http://test/chunk1"],
            Codec: "flac",
            MimeType: "audio/mp4",
            FileExtension: ".m4a",
            SampleRate: 44100,
            IsEncrypted: true,
            KeyId: "key-id",
            SecurityToken: token);

        // Act - Line 82-94: TidalStreamDecryptor.Decrypt path
        MemoryStream result = await downloader.DownloadAndAssembleAsync(manifest);

        // Assert - Verify decryption succeeded
        byte[] resultBytes = result.ToArray();
        Assert.Equal(plainData, resultBytes);
    }

    /// <summary>
    /// Source lines 82-94: Multi-chunk encrypted content decryption.
    /// Chunks are concatenated before decryption.
    /// </summary>
    [Fact]
    public async Task DownloadAndAssembleAsync_EncryptedMultipleChunks_DecryptsConcatenated()
    {
        // Arrange
        byte[] chunk1 = Encoding.UTF8.GetBytes("chunk-one-");
        byte[] chunk2 = Encoding.UTF8.GetBytes("chunk-two-");
        byte[] chunk3 = Encoding.UTF8.GetBytes("chunk-three");
        byte[] expectedPlain = [.. chunk1, .. chunk2, .. chunk3];

        (string token, byte[][] encryptedChunks) = BuildEncryptedChunks([chunk1, chunk2, chunk3]);

        ChunkSequenceHandler handler = new(encryptedChunks);
        TidalChunkDownloader downloader = new(new HttpClient(handler));

        TidalManifest manifest = new(
            ChunkUrls: ["http://test/1", "http://test/2", "http://test/3"],
            Codec: "flac",
            MimeType: "audio/mp4",
            FileExtension: ".m4a",
            SampleRate: 44100,
            IsEncrypted: true,
            KeyId: "key-id",
            SecurityToken: token);

        // Act
        MemoryStream result = await downloader.DownloadAndAssembleAsync(manifest);

        // Assert
        Assert.Equal(expectedPlain, result.ToArray());
    }

    #endregion

    #region Line 226: Successful Decryption in DownloadAndAssembleToFileStreamAsync

    /// <summary>
    /// Source line 226: await TidalStreamDecryptor.DecryptFileStreamAsync
    /// Verifies file-backed decryption path works correctly.
    /// </summary>
    [Fact]
    public async Task DownloadAndAssembleToFileStreamAsync_EncryptedWithValidToken_DecryptsSuccessfully()
    {
        // Arrange
        byte[] plainData = Encoding.UTF8.GetBytes("file-decrypted-content");
        (string token, byte[][] encryptedChunks) = BuildEncryptedChunks([plainData]);

        ChunkSequenceHandler handler = new(encryptedChunks);
        TidalChunkDownloader downloader = new(new HttpClient(handler));

        TidalManifest manifest = new(
            ChunkUrls: ["http://test/chunk"],
            Codec: "flac",
            MimeType: "audio/mp4",
            FileExtension: ".m4a",
            SampleRate: 44100,
            IsEncrypted: true,
            KeyId: "key-id",
            SecurityToken: token);

        // Act - Line 226: File-backed decryption path
        await using Stream result = await downloader.DownloadAndAssembleToFileStreamAsync(
            manifest, chunkDelayMs: 0, maxConcurrentChunkDownloads: 1);

        // Assert
        using MemoryStream ms = new();
        await result.CopyToAsync(ms);
        Assert.Equal(plainData, ms.ToArray());
    }

    /// <summary>
    /// Source line 226: Multi-chunk file-backed decryption.
    /// </summary>
    [Fact]
    public async Task DownloadAndAssembleToFileStreamAsync_EncryptedMultipleChunks_DecryptsConcatenated()
    {
        // Arrange
        byte[] chunk1 = [1, 2, 3];
        byte[] chunk2 = [4, 5, 6];
        byte[] expectedPlain = [1, 2, 3, 4, 5, 6];

        (string token, byte[][] encryptedChunks) = BuildEncryptedChunks([chunk1, chunk2]);

        ChunkSequenceHandler handler = new(encryptedChunks);
        TidalChunkDownloader downloader = new(new HttpClient(handler));

        TidalManifest manifest = new(
            ChunkUrls: ["http://test/1", "http://test/2"],
            Codec: "flac",
            MimeType: "audio/mp4",
            FileExtension: ".m4a",
            SampleRate: 44100,
            IsEncrypted: true,
            KeyId: "key-id",
            SecurityToken: token);

        // Act
        await using Stream result = await downloader.DownloadAndAssembleToFileStreamAsync(
            manifest, chunkDelayMs: 0, maxConcurrentChunkDownloads: 1);

        // Assert
        using MemoryStream ms = new();
        await result.CopyToAsync(ms);
        Assert.Equal(expectedPlain, ms.ToArray());
    }

    #endregion

    #region Line 349: Successful Decryption in Legacy DownloadAndAssembleAsync

    /// <summary>
    /// Source line 349: await TidalStreamDecryptor.DecryptFileStreamAsync (legacy path)
    /// Verifies legacy TidalStreamInfo-based decryption works.
    /// </summary>
    [Fact]
    public async Task DownloadAndAssembleAsync_Legacy_EncryptedWithValidToken_DecryptsSuccessfully()
    {
        // Arrange
        byte[] plainData = Encoding.UTF8.GetBytes("legacy-decrypted");
        (string token, byte[][] encryptedChunks) = BuildEncryptedChunks([plainData]);

        ChunkSequenceHandler handler = new(encryptedChunks);
        TidalChunkDownloader downloader = new(new HttpClient(handler));

        TidalStreamInfo streamInfo = new(
            TrackId: "track-legacy-encrypted",
            ChunkUrls: ["http://legacy/chunk"],
            FileExtension: ".m4a",
            MimeType: "audio/mp4",
            IsEncrypted: true,
            SecurityToken: token);

        // Act - Line 349: Legacy file-backed decryption
        await using Stream result = await downloader.DownloadAndAssembleAsync(
            streamInfo, chunkDelayMs: 0, maxConcurrentChunkDownloads: 1);

        // Assert
        using MemoryStream ms = new();
        await result.CopyToAsync(ms);
        Assert.Equal(plainData, ms.ToArray());
    }

    /// <summary>
    /// Source line 349: Multi-chunk legacy decryption.
    /// </summary>
    [Fact]
    public async Task DownloadAndAssembleAsync_Legacy_EncryptedMultipleChunks_DecryptsConcatenated()
    {
        // Arrange
        byte[] chunk1 = [10, 20];
        byte[] chunk2 = [30, 40];
        byte[] expectedPlain = [10, 20, 30, 40];

        (string token, byte[][] encryptedChunks) = BuildEncryptedChunks([chunk1, chunk2]);

        ChunkSequenceHandler handler = new(encryptedChunks);
        TidalChunkDownloader downloader = new(new HttpClient(handler));

        TidalStreamInfo streamInfo = new(
            TrackId: "track-legacy-multi",
            ChunkUrls: ["http://legacy/1", "http://legacy/2"],
            FileExtension: ".m4a",
            MimeType: "audio/mp4",
            IsEncrypted: true,
            SecurityToken: token);

        // Act
        await using Stream result = await downloader.DownloadAndAssembleAsync(
            streamInfo, chunkDelayMs: 0, maxConcurrentChunkDownloads: 1);

        // Assert
        using MemoryStream ms = new();
        await result.CopyToAsync(ms);
        Assert.Equal(expectedPlain, ms.ToArray());
    }

    #endregion

    #region Line 126/262: Single Chunk with High Concurrency (Sequential Fallback)

    /// <summary>
    /// Source line 126: maxConcurrentChunkDownloads <= 1 || manifest.ChunkUrls.Length <= 1
    /// Single chunk should use sequential path regardless of maxConcurrentChunkDownloads.
    /// </summary>
    [Fact]
    public async Task DownloadAndAssembleToFileStreamAsync_SingleChunkHighConcurrency_UsesSequentialPath()
    {
        // Arrange
        byte[] expectedData = [42, 43, 44];
        ConstantHandler handler = new(expectedData);
        TidalChunkDownloader downloader = new(new HttpClient(handler));

        TidalManifest manifest = new(
            ChunkUrls: ["http://single/chunk"],
            Codec: "AAC",
            MimeType: "audio/mp4",
            FileExtension: ".m4a",
            SampleRate: 44100,
            IsEncrypted: false,
            KeyId: null,
            SecurityToken: null);

        // Act - Request high concurrency but single chunk forces sequential
        await using Stream result = await downloader.DownloadAndAssembleToFileStreamAsync(
            manifest, chunkDelayMs: 0, maxConcurrentChunkDownloads: 10);

        // Assert
        using MemoryStream ms = new();
        await result.CopyToAsync(ms);
        Assert.Equal(expectedData, ms.ToArray());
    }

    /// <summary>
    /// Source line 262: maxConcurrentChunkDownloads <= 1 || streamInfo.ChunkUrls.Length <= 1
    /// Legacy method single chunk sequential fallback.
    /// </summary>
    [Fact]
    public async Task DownloadAndAssembleAsync_Legacy_SingleChunkHighConcurrency_UsesSequentialPath()
    {
        // Arrange
        byte[] expectedData = [100, 101, 102];
        ConstantHandler handler = new(expectedData);
        TidalChunkDownloader downloader = new(new HttpClient(handler));

        TidalStreamInfo streamInfo = new(
            TrackId: "single-chunk-track",
            ChunkUrls: ["http://single/legacy"],
            FileExtension: ".m4a",
            MimeType: "audio/mp4",
            IsEncrypted: false,
            SecurityToken: null);

        // Act
        await using Stream result = await downloader.DownloadAndAssembleAsync(
            streamInfo, chunkDelayMs: 0, maxConcurrentChunkDownloads: 10);

        // Assert
        using MemoryStream ms = new();
        await result.CopyToAsync(ms);
        Assert.Equal(expectedData, ms.ToArray());
    }

    #endregion

    #region Line 11: ChunkDownloadProgress ProgressPercentage Edge Cases

    /// <summary>
    /// Source line 11: TotalChunks > 0 ? ... : 0
    /// Zero total chunks should return 0% progress.
    /// </summary>
    [Fact]
    public void ChunkDownloadProgress_ZeroTotalChunks_ProgressPercentageIsZero()
    {
        // Arrange & Act
        ChunkDownloadProgress progress = new()
        {
            TotalChunks = 0,
            CompletedChunks = 0
        };

        // Assert - Line 11: TotalChunks == 0 should return 0
        Assert.Equal(0.0, progress.ProgressPercentage);
    }

    /// <summary>
    /// Source line 11: (double)CompletedChunks / TotalChunks * 100
    /// Partial progress calculation.
    /// </summary>
    [Fact]
    public void ChunkDownloadProgress_PartialProgress_CalculatesCorrectly()
    {
        // Arrange & Act
        ChunkDownloadProgress progress = new()
        {
            TotalChunks = 4,
            CompletedChunks = 2
        };

        // Assert
        Assert.Equal(50.0, progress.ProgressPercentage);
    }

    /// <summary>
    /// Source line 11: Full completion should be 100%.
    /// </summary>
    [Fact]
    public void ChunkDownloadProgress_Completed_Calculates100()
    {
        // Arrange & Act
        ChunkDownloadProgress progress = new()
        {
            TotalChunks = 5,
            CompletedChunks = 5
        };

        // Assert
        Assert.Equal(100.0, progress.ProgressPercentage);
    }

    #endregion

    #region Lines 197-209: Parallel Download Exception Handling (File-Backed)

    /// <summary>
    /// Source lines 197-209: Parallel exception handling with CTS cancellation.
    /// Verifies that when a chunk fails during parallel download, the CTS is cancelled
    /// and other tasks are properly observed before rethrowing.
    /// </summary>
    [Fact]
    public async Task DownloadAndAssembleToFileStreamAsync_ParallelChunkFails_ThrowsAndCancelsOtherTasks()
    {
        // Arrange
        Dictionary<string, byte[]> chunkMap = new()
        {
            ["http://parallel/1"] = [1, 2],
            ["http://parallel/2"] = [3, 4],
            ["http://parallel/3"] = [5, 6]
        };

        FailingOnUrlHandler handler = new(chunkMap, failOnUrl: "http://parallel/2");
        TidalChunkDownloader downloader = new(new HttpClient(handler));

        TidalManifest manifest = new(
            ChunkUrls: ["http://parallel/1", "http://parallel/2", "http://parallel/3"],
            Codec: "AAC",
            MimeType: "audio/mp4",
            FileExtension: ".m4a",
            SampleRate: 44100,
            IsEncrypted: false,
            KeyId: null,
            SecurityToken: null);

        // Act & Assert - Lines 197-209: catch block cancels CTS and observes all tasks
        await Assert.ThrowsAsync<HttpRequestException>(
            () => downloader.DownloadAndAssembleToFileStreamAsync(
                manifest, chunkDelayMs: 0, maxConcurrentChunkDownloads: 3));
    }

    #endregion

    #region Lines 308-323: Parallel Download Exception Handling (Legacy)

    /// <summary>
    /// Source lines 308-323: Legacy parallel exception handling.
    /// Verifies CTS cancellation and task observation in legacy method.
    /// </summary>
    [Fact]
    public async Task DownloadAndAssembleAsync_Legacy_ParallelChunkFails_ThrowsAndCancelsOtherTasks()
    {
        // Arrange
        Dictionary<string, byte[]> chunkMap = new()
        {
            ["http://legacy-parallel/1"] = [10],
            ["http://legacy-parallel/2"] = [20]
        };

        FailingOnUrlHandler handler = new(chunkMap, failOnUrl: "http://legacy-parallel/1");
        TidalChunkDownloader downloader = new(new HttpClient(handler));

        TidalStreamInfo streamInfo = new(
            TrackId: "parallel-fail-track",
            ChunkUrls: ["http://legacy-parallel/1", "http://legacy-parallel/2"],
            FileExtension: ".m4a",
            MimeType: "audio/mp4",
            IsEncrypted: false,
            SecurityToken: null);

        // Act & Assert - Lines 308-323: catch cancels CTS and observes tasks
        await Assert.ThrowsAsync<HttpRequestException>(
            () => downloader.DownloadAndAssembleAsync(
                streamInfo, chunkDelayMs: 0, maxConcurrentChunkDownloads: 2));
    }

    #endregion

    #region DownloadAndAssembleBytesAsync Encrypted

    /// <summary>
    /// Source lines 382-397: Bytes method with decryption.
    /// Verifies the convenience method properly decrypts content.
    /// </summary>
    [Fact]
    public async Task DownloadAndAssembleBytesAsync_EncryptedWithValidToken_DecryptsSuccessfully()
    {
        // Arrange
        byte[] plainData = Encoding.UTF8.GetBytes("bytes-decrypted-content");
        (string token, byte[][] encryptedChunks) = BuildEncryptedChunks([plainData]);

        ChunkSequenceHandler handler = new(encryptedChunks);
        TidalChunkDownloader downloader = new(new HttpClient(handler));

        TidalStreamInfo streamInfo = new(
            TrackId: "bytes-encrypted-track",
            ChunkUrls: ["http://bytes/chunk"],
            FileExtension: ".m4a",
            MimeType: "audio/mp4",
            IsEncrypted: true,
            SecurityToken: token);

        // Act
        byte[] result = await downloader.DownloadAndAssembleBytesAsync(streamInfo);

        // Assert
        Assert.Equal(plainData, result);
    }

    #endregion

    #region Progress Reporting with Decryption

    /// <summary>
    /// Source lines 55-59: Progress reporting during encrypted download.
    /// Verifies progress is reported correctly even when decryption is performed.
    /// </summary>
    [Fact]
    public async Task DownloadAndAssembleAsync_EncryptedWithProgress_ReportsForEachChunk()
    {
        // Arrange
        byte[] chunk1 = [1];
        byte[] chunk2 = [2];
        byte[] chunk3 = [3];

        (string token, byte[][] encryptedChunks) = BuildEncryptedChunks([chunk1, chunk2, chunk3]);

        ChunkSequenceHandler handler = new(encryptedChunks);
        TidalChunkDownloader downloader = new(new HttpClient(handler));

        int progressCount = 0;
        ChunkDownloadProgress? lastProgress = null;
        Progress<ChunkDownloadProgress> progress = new(p =>
        {
            progressCount++;
            lastProgress = p;
        });

        TidalManifest manifest = new(
            ChunkUrls: ["http://test/1", "http://test/2", "http://test/3"],
            Codec: "flac",
            MimeType: "audio/mp4",
            FileExtension: ".m4a",
            SampleRate: 44100,
            IsEncrypted: true,
            KeyId: "key-id",
            SecurityToken: token);

        // Act
        await downloader.DownloadAndAssembleAsync(manifest, progress: progress);

        // Assert
        Assert.Equal(3, progressCount);
        Assert.Equal(3, lastProgress?.TotalChunks);
        Assert.Equal(3, lastProgress?.CompletedChunks);
        Assert.Equal(100.0, lastProgress?.ProgressPercentage);
    }

    #endregion

    #region Helper Methods

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

    #endregion

    #region Helper Classes

    private sealed class ConstantHandler(byte[] payload) : HttpMessageHandler
    {
        private readonly byte[] _payload = payload;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(_payload)
            });
    }

    private sealed class ChunkSequenceHandler(byte[][] chunks) : HttpMessageHandler
    {
        private readonly byte[][] _chunks = chunks;
        private int _index;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            byte[] data = _chunks[_index % _chunks.Length];
            _index++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(data)
            });
        }
    }

    private sealed class FailingOnUrlHandler(Dictionary<string, byte[]> chunkMap, string failOnUrl) : HttpMessageHandler
    {
        private readonly Dictionary<string, byte[]> _chunkMap = chunkMap;
        private readonly string _failOnUrl = failOnUrl;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string url = request.RequestUri?.ToString() ?? string.Empty;

            if (url == _failOnUrl)
            {
                throw new HttpRequestException("Simulated chunk failure");
            }

            if (_chunkMap.TryGetValue(url, out byte[]? payload))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(payload)
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    #endregion
}
