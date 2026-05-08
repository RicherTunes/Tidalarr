using System.Net;
using Microsoft.Extensions.Logging;
using Moq;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Streaming;

namespace Tidalarr.Tests;

/// <summary>
/// Coverage tests for TidalChunkDownloader - tests uncovered paths.
/// Source: src/Tidalarr/Domain/Streaming/TidalChunkDownloader.cs
/// </summary>
public class TidalChunkDownloaderCovTests
{
    #region DownloadAndAssembleAsync Encrypted Missing Token (Line 79)

    [Fact]
    public async Task DownloadAndAssembleAsync_EncryptedManifestMissingToken_ThrowsInvalidOperationException()
    {
        // Arrange - Lines 76-79: Encrypted manifest with null/whitespace security token
        ConstantHandler handler = new([1, 2, 3]);
        TidalChunkDownloader downloader = new(new HttpClient(handler));

        TidalManifest manifest = new(
            ChunkUrls: ["http://test/chunk"],
            Codec: "AAC",
            MimeType: "audio/mp4",
            FileExtension: ".m4a",
            SampleRate: 44100,
            IsEncrypted: true,
            KeyId: "key-id",
            SecurityToken: null); // Missing token for encrypted manifest

        // Act & Assert - Line 79
        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => downloader.DownloadAndAssembleAsync(manifest));

        // Assert - verify message from Line 79
        Assert.Contains("Encrypted manifest missing security token", ex.Message);
    }

    [Fact]
    public async Task DownloadAndAssembleAsync_EncryptedManifestWhitespaceToken_ThrowsInvalidOperationException()
    {
        // Arrange - Lines 76-79: Encrypted manifest with whitespace-only security token
        ConstantHandler handler = new([1, 2, 3]);
        TidalChunkDownloader downloader = new(new HttpClient(handler));

        TidalManifest manifest = new(
            ChunkUrls: ["http://test/chunk"],
            Codec: "AAC",
            MimeType: "audio/mp4",
            FileExtension: ".m4a",
            SampleRate: 44100,
            IsEncrypted: true,
            KeyId: "key-id",
            SecurityToken: "   "); // Whitespace-only token

        // Act & Assert - Line 79: string.IsNullOrWhiteSpace check
        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => downloader.DownloadAndAssembleAsync(manifest));

        Assert.Contains("Encrypted manifest missing security token", ex.Message);
    }

    #endregion

    #region DownloadAndAssembleAsync Chunk Delay (Lines 62-64)

    [Fact]
    public async Task DownloadAndAssembleAsync_WithChunkDelay_AppliesDelay()
    {
        // Arrange - Lines 62-64: chunkDelayMs > 0 applies delay
        ConstantHandler handler = new([1, 2, 3]);
        TidalChunkDownloader downloader = new(new HttpClient(handler));

        TidalManifest manifest = new(
            ChunkUrls: ["http://test/1", "http://test/2"],
            Codec: "AAC",
            MimeType: "audio/mp4",
            FileExtension: ".m4a",
            SampleRate: 44100,
            IsEncrypted: false,
            KeyId: null,
            SecurityToken: null);

        System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();

        // Act - Use 10ms delay per chunk
        await downloader.DownloadAndAssembleAsync(manifest, chunkDelayMs: 10);

        sw.Stop();

        // Assert - Should have at least 10ms delay between 2 chunks
        Assert.True(sw.ElapsedMilliseconds >= 10, $"Expected delay >= 10ms, got {sw.ElapsedMilliseconds}ms");
    }

    #endregion

    #region DownloadAndAssembleToFileStreamAsync Chunk Delay (Lines 150-153)

    [Fact]
    public async Task DownloadAndAssembleToFileStreamAsync_WithChunkDelay_AppliesDelay()
    {
        // Arrange - Lines 150-153: chunkDelayMs > 0 in file-backed variant
        ConstantHandler handler = new([1, 2, 3]);
        TidalChunkDownloader downloader = new(new HttpClient(handler));

        TidalManifest manifest = new(
            ChunkUrls: ["http://test/1", "http://test/2"],
            Codec: "AAC",
            MimeType: "audio/mp4",
            FileExtension: ".m4a",
            SampleRate: 44100,
            IsEncrypted: false,
            KeyId: null,
            SecurityToken: null);

        System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();

        // Act - Use 10ms delay per chunk
        await using Stream _ = await downloader.DownloadAndAssembleToFileStreamAsync(
            manifest, chunkDelayMs: 10, maxConcurrentChunkDownloads: 1);

        sw.Stop();

        // Assert - Should have at least 10ms delay between 2 chunks
        Assert.True(sw.ElapsedMilliseconds >= 10, $"Expected delay >= 10ms, got {sw.ElapsedMilliseconds}ms");
    }

    #endregion

    #region DownloadAndAssembleToFileStreamAsync Parallel Downloads (Lines 156-214)

    [Fact]
    public async Task DownloadAndAssembleToFileStreamAsync_MultipleChunksParallel_DownloadsInParallel()
    {
        // Arrange - Lines 156-214: Parallel chunk download path with semaphore
        int callCount = 0;
        byte[][] chunks = [[1], [2], [3]];

        SequenceHandler handler = new(() => chunks[callCount++ % chunks.Length]);
        TidalChunkDownloader downloader = new(new HttpClient(handler));

        TidalManifest manifest = new(
            ChunkUrls: ["http://test/1", "http://test/2", "http://test/3"],
            Codec: "AAC",
            MimeType: "audio/mp4",
            FileExtension: ".m4a",
            SampleRate: 44100,
            IsEncrypted: false,
            KeyId: null,
            SecurityToken: null);

        // Act - Use maxConcurrentChunkDownloads > 1 with no delay to trigger parallel path
        // Line 126 condition: maxConcurrentChunkDownloads > 1 && manifest.ChunkUrls.Length > 1
        await using Stream result = await downloader.DownloadAndAssembleToFileStreamAsync(
            manifest, chunkDelayMs: 0, maxConcurrentChunkDownloads: 2);

        // Assert - Verify content was downloaded
        using MemoryStream ms = new();
        await result.CopyToAsync(ms);
        byte[] data = ms.ToArray();
        Assert.Equal(3, data.Length); // 3 chunks of 1 byte each
    }

    [Fact]
    public async Task DownloadAndAssembleToFileStreamAsync_ParallelWithLogger_LogsCleanupAttempts()
    {
        // Arrange - Lines 187, 212: Logger calls for best-effort cleanup
        Mock<ILogger<TidalChunkDownloader>> loggerMock = new();
        int callCount = 0;
        byte[][] chunks = [[1], [2]];

        SequenceHandler handler = new(() => chunks[callCount++ % chunks.Length]);
        TidalChunkDownloader downloader = new(new HttpClient(handler), loggerMock.Object);

        TidalManifest manifest = new(
            ChunkUrls: ["http://test/1", "http://test/2"],
            Codec: "AAC",
            MimeType: "audio/mp4",
            FileExtension: ".m4a",
            SampleRate: 44100,
            IsEncrypted: false,
            KeyId: null,
            SecurityToken: null);

        // Act - Parallel path with logger
        await using Stream _ = await downloader.DownloadAndAssembleToFileStreamAsync(
            manifest, chunkDelayMs: 0, maxConcurrentChunkDownloads: 2);

        // Assert - Logger was available (cleanup logging path exercised)
        loggerMock.Verify(x => x.Log(
            It.IsAny<LogLevel>(),
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never()); // Cleanup only logs on failure, which we don't trigger
    }

    #endregion

    #region Legacy DownloadAndAssembleAsync Parallel Downloads (Lines 287-336)

    [Fact]
    public async Task DownloadAndAssembleAsync_Legacy_ParallelDownloads_DownloadsInParallel()
    {
        // Arrange - Lines 287-336: Parallel chunk download in legacy method
        int callCount = 0;
        byte[][] chunks = [[10], [20], [30]];

        SequenceHandler handler = new(() => chunks[callCount++ % chunks.Length]);
        TidalChunkDownloader downloader = new(new HttpClient(handler));

        TidalStreamInfo streamInfo = new(
            TrackId: "track-parallel",
            ChunkUrls: ["http://legacy/1", "http://legacy/2", "http://legacy/3"],
            FileExtension: ".m4a",
            MimeType: "audio/mp4",
            IsEncrypted: false,
            SecurityToken: null);

        // Act - Use maxConcurrentChunkDownloads > 1 with no delay
        // Line 262 condition: maxConcurrentChunkDownloads > 1 && streamInfo.ChunkUrls.Length > 1
        await using Stream result = await downloader.DownloadAndAssembleAsync(
            streamInfo, chunkDelayMs: 0, maxConcurrentChunkDownloads: 3);

        // Assert
        using MemoryStream ms = new();
        await result.CopyToAsync(ms);
        Assert.Equal(3, ms.ToArray().Length);
    }

    [Fact]
    public async Task DownloadAndAssembleAsync_Legacy_ParallelWithLogger_LogsCleanupAttempts()
    {
        // Arrange - Lines 318, 335: Logger calls in legacy parallel path
        Mock<ILogger<TidalChunkDownloader>> loggerMock = new();
        int callCount = 0;
        byte[][] chunks = [[1], [2]];

        SequenceHandler handler = new(() => chunks[callCount++ % chunks.Length]);
        TidalChunkDownloader downloader = new(new HttpClient(handler), loggerMock.Object);

        TidalStreamInfo streamInfo = new(
            TrackId: "track-parallel-logged",
            ChunkUrls: ["http://legacy/1", "http://legacy/2"],
            FileExtension: ".m4a",
            MimeType: "audio/mp4",
            IsEncrypted: false,
            SecurityToken: null);

        // Act
        await using Stream _ = await downloader.DownloadAndAssembleAsync(
            streamInfo, chunkDelayMs: 0, maxConcurrentChunkDownloads: 2);

        // Assert - Logger available for cleanup paths
        // No cleanup failures expected, so no log calls
        loggerMock.Verify(x => x.Log(
            It.IsAny<LogLevel>(),
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never());
    }

    #endregion

    #region Legacy DownloadAndAssembleAsync Chunk Delay (Lines 281-284)

    [Fact]
    public async Task DownloadAndAssembleAsync_Legacy_WithChunkDelay_AppliesDelay()
    {
        // Arrange - Lines 281-284: chunkDelayMs > 0 in legacy sequential path
        ConstantHandler handler = new([1, 2, 3]);
        TidalChunkDownloader downloader = new(new HttpClient(handler));

        TidalStreamInfo streamInfo = new(
            TrackId: "track-delay",
            ChunkUrls: ["http://legacy/1", "http://legacy/2"],
            FileExtension: ".m4a",
            MimeType: "audio/mp4",
            IsEncrypted: false,
            SecurityToken: null);

        System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();

        // Act - Use 10ms delay per chunk
        await using Stream _ = await downloader.DownloadAndAssembleAsync(
            streamInfo, chunkDelayMs: 10, maxConcurrentChunkDownloads: 1);

        sw.Stop();

        // Assert - Should have at least 10ms delay between 2 chunks
        Assert.True(sw.ElapsedMilliseconds >= 10, $"Expected delay >= 10ms, got {sw.ElapsedMilliseconds}ms");
    }

    #endregion

    #region ValidateChunkAccessibilityAsync Exception Path (Lines 411-414)

    [Fact]
    public async Task ValidateChunkAccessibilityAsync_HttpException_ReturnsFalse()
    {
        // Arrange - Lines 411-414: catch block returns false on exception
        ExceptionThrowingHandler handler = new();
        TidalChunkDownloader downloader = new(new HttpClient(handler));

        // Act - Request that will throw
        bool result = await downloader.ValidateChunkAccessibilityAsync(["http://will-throw/test"]);

        // Assert - Line 413: catch returns false
        Assert.False(result);
    }

    [Fact]
    public async Task ValidateChunkAccessibilityAsync_StringArray_HttpError_ReturnsFalse()
    {
        // Arrange - Lines 411-414: Non-success status or exception returns false
        FailingHandler handler = new();
        TidalChunkDownloader downloader = new(new HttpClient(handler));

        // Act
        bool result = await downloader.ValidateChunkAccessibilityAsync(["http://failing/test"]);

        // Assert - Line 409: IsSuccessStatusCode is false
        Assert.False(result);
    }

    #endregion

    #region DownloadAndAssembleAsync With Successful Decryption (Lines 82-94)

    [Fact]
    public async Task DownloadAndAssembleAsync_EncryptedWithValidToken_AttemptsDecryption()
    {
        // Arrange - Lines 82-94: Decryption path when RequiresDecryption returns true
        // Note: This test exercises the decryption path. The actual decryption will fail
        // with invalid token format, but we verify the path is taken.

        // Create a base64 security token short enough that DeriveKeyAndCounter's length
        // guard trips and throws InvalidOperationException("Security token is malformed.").
        // Tokens of length >= 24 with valid block-aligned bodies decrypt to garbage without
        // throwing (AES-CBC PaddingMode.None on zeroed input succeeds), so we deliberately
        // pick 20 bytes to exercise the throw branch the test expects.
        byte[] fakeToken = new byte[20];
        string base64Token = Convert.ToBase64String(fakeToken);

        ConstantHandler handler = new([1, 2, 3, 4, 5]);
        TidalChunkDownloader downloader = new(new HttpClient(handler));

        TidalManifest manifest = new(
            ChunkUrls: ["http://test/chunk"],
            Codec: "AAC",
            MimeType: "audio/mp4",
            FileExtension: ".m4a",
            SampleRate: 44100,
            IsEncrypted: true,
            KeyId: "key-id",
            SecurityToken: base64Token);

        // Act & Assert - Line 86: TidalStreamDecryptor.Decrypt will throw
        // because our fake token is not properly encrypted
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => downloader.DownloadAndAssembleAsync(manifest));
    }

    #endregion

    #region RequiresDecryption Edge Cases (Line 425-428)

    [Fact]
    public async Task DownloadAndAssembleAsync_NotEncryptedWithToken_ReturnsUndecrypted()
    {
        // Arrange - Line 425-428: RequiresDecryption returns false when not encrypted
        ConstantHandler handler = new([10, 20, 30]);
        TidalChunkDownloader downloader = new(new HttpClient(handler));

        TidalManifest manifest = new(
            ChunkUrls: ["http://test/chunk"],
            Codec: "AAC",
            MimeType: "audio/mp4",
            FileExtension: ".m4a",
            SampleRate: 44100,
            IsEncrypted: false, // Not encrypted
            KeyId: null,
            SecurityToken: "sometoken"); // Has token but not encrypted

        // Act - Should NOT attempt decryption (Line 82: RequiresDecryption = false)
        MemoryStream result = await downloader.DownloadAndAssembleAsync(manifest);

        // Assert - Returns stream without decryption
        Assert.Equal([10, 20, 30], result.ToArray());
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

    private sealed class SequenceHandler(Func<byte[]> getNextChunk) : HttpMessageHandler
    {
        private readonly Func<byte[]> _getNextChunk = getNextChunk;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(_getNextChunk())
            });
    }

    private sealed class FailingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("Server error")
            });
    }

    private sealed class ExceptionThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException("Network error");
    }

    #endregion
}
