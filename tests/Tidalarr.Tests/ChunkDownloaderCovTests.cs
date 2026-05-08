using System.Net;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Streaming;

namespace Tidalarr.Tests;

/// <summary>
/// Coverage tests for TidalChunkDownloader - tests uncovered paths.
/// Source: src/Tidalarr/Domain/Streaming/TidalChunkDownloader.cs
/// </summary>
public class ChunkDownloaderCovTests
{
    #region ChunkDownloadProgress Tests (Line 7-12)

    [Fact]
    public void ChunkDownloadProgress_ProgressPercentage_WhenTotalChunksZero_ReturnsZero()
    {
        // Arrange & Act - Line 11: ProgressPercentage when TotalChunks is 0
        ChunkDownloadProgress progress = new() { TotalChunks = 0, CompletedChunks = 0 };

        // Assert
        Assert.Equal(0.0, progress.ProgressPercentage);
    }

    [Fact]
    public void ChunkDownloadProgress_ProgressPercentage_WhenHalfComplete_ReturnsFifty()
    {
        // Arrange & Act - Line 11: ProgressPercentage calculation
        ChunkDownloadProgress progress = new() { TotalChunks = 10, CompletedChunks = 5 };

        // Assert
        Assert.Equal(50.0, progress.ProgressPercentage);
    }

    [Fact]
    public void ChunkDownloadProgress_ProgressPercentage_WhenComplete_ReturnsHundred()
    {
        // Arrange & Act - Line 11: ProgressPercentage at 100%
        ChunkDownloadProgress progress = new() { TotalChunks = 4, CompletedChunks = 4 };

        // Assert
        Assert.Equal(100.0, progress.ProgressPercentage);
    }

    #endregion

    #region DownloadAndAssembleAsync Progress Reporting Tests (Lines 55-59)

    // Removed: DownloadAndAssembleAsync_WithProgress_ReportsProgress
    // Progress<T> fires callbacks on captured SynchronizationContext, not synchronously.
    // This caused non-deterministic failures in test environments.

    #endregion

    #region DownloadAndAssembleAsync Chunk Failure Tests (Line 70)

    [Fact]
    public async Task DownloadAndAssembleAsync_WhenChunkFails_ThrowsInvalidOperationException()
    {
        // Arrange - Line 70: InvalidOperationException on chunk failure
        FailingHandler handler = new();
        TidalChunkDownloader downloader = new(new HttpClient(handler));

        TidalManifest manifest = new(
            ChunkUrls: ["https://failing-chunk.test"],
            Codec: "flac",
            MimeType: "audio/mp4",
            FileExtension: ".flac",
            SampleRate: 44100,
            IsEncrypted: false,
            KeyId: null,
            SecurityToken: null);

        // Act & Assert
        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => downloader.DownloadAndAssembleAsync(manifest));

        // Assert - Line 70: verify message contains chunk URL
        Assert.Contains("https://failing-chunk.test", ex.Message);
        Assert.Contains("Failed to download chunk", ex.Message);
    }

    #endregion

    #region DownloadAndAssembleToFileStreamAsync Tests (Lines 104-235)

    [Fact]
    public async Task DownloadAndAssembleToFileStreamAsync_WithProgress_ReportsProgress()
    {
        // Arrange - Lines 144-148: Progress reporting in file-backed variant
        int callCount = 0;
        byte[][] chunks = [[1], [2]];

        SequenceHandler handler = new(() => chunks[callCount++]);
        TidalChunkDownloader downloader = new(new HttpClient(handler));
        List<ChunkDownloadProgress> progressReports = [];

        TidalManifest manifest = new(
            ChunkUrls: ["http://test/1", "http://test/2"],
            Codec: "AAC",
            MimeType: "audio/mp4",
            FileExtension: "m4a",
            SampleRate: 44100,
            IsEncrypted: false,
            KeyId: null,
            SecurityToken: null);

        // Act
        Progress<ChunkDownloadProgress> progress = new(p => progressReports.Add(p));
        await using Stream _ = await downloader.DownloadAndAssembleToFileStreamAsync(
            manifest, chunkDelayMs: 0, maxConcurrentChunkDownloads: 1, progress: progress);

        // Assert
        Assert.Equal(2, progressReports.Count);
        Assert.Equal(1, progressReports[0].CompletedChunks);
        Assert.Equal(2, progressReports[1].CompletedChunks);
    }

    [Fact]
    public async Task DownloadAndAssembleToFileStreamAsync_EncryptedMissingToken_Throws()
    {
        // Arrange - Lines 216-218: Encrypted manifest missing security token
        ConstantHandler handler = new([1, 2, 3]);
        TidalChunkDownloader downloader = new(new HttpClient(handler));

        TidalManifest manifest = new(
            ChunkUrls: ["http://test/1"],
            Codec: "AAC",
            MimeType: "audio/mp4",
            FileExtension: "m4a",
            SampleRate: 44100,
            IsEncrypted: true,
            KeyId: "key-id",
            SecurityToken: null); // Missing token for encrypted manifest

        // Act & Assert - Line 218
        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => downloader.DownloadAndAssembleToFileStreamAsync(manifest));

        Assert.Contains("Encrypted manifest missing security token", ex.Message);
    }

    #endregion

    #region Legacy DownloadAndAssembleAsync(TidalStreamInfo) Tests (Lines 244-357)

    [Fact]
    public async Task DownloadAndAssembleAsync_Legacy_ValidStreamInfo_ReturnsStream()
    {
        // Arrange - Lines 244-357: Legacy method with TidalStreamInfo
        int callCount = 0;
        byte[][] chunks = [[10, 20], [30, 40]];

        SequenceHandler handler = new(() => chunks[callCount++]);
        TidalChunkDownloader downloader = new(new HttpClient(handler));

        TidalStreamInfo streamInfo = new(
            TrackId: "track-123",
            ChunkUrls: ["https://legacy/1", "https://legacy/2"],
            FileExtension: ".flac",
            MimeType: "audio/flac",
            IsEncrypted: false,
            SecurityToken: null);

        // Act
        await using Stream result = await downloader.DownloadAndAssembleAsync(streamInfo);

        // Assert
        using MemoryStream ms = new();
        await result.CopyToAsync(ms);
        Assert.Equal([10, 20, 30, 40], ms.ToArray());
    }

    [Fact]
    public async Task DownloadAndAssembleAsync_Legacy_WithProgress_ReportsProgress()
    {
        // Arrange - Line 279: Progress reporting with IProgress<int>
        int callCount = 0;
        byte[][] chunks = [[1], [2], [3]];

        SequenceHandler handler = new(() => chunks[callCount++]);
        TidalChunkDownloader downloader = new(new HttpClient(handler));
        List<int> progressReports = [];

        TidalStreamInfo streamInfo = new(
            TrackId: "track-456",
            ChunkUrls: ["https://prog/1", "https://prog/2", "https://prog/3"],
            FileExtension: ".m4a",
            MimeType: "audio/mp4",
            IsEncrypted: false,
            SecurityToken: null);

        // Act
        Progress<int> progress = new(p => progressReports.Add(p));
        await using Stream _ = await downloader.DownloadAndAssembleAsync(
            streamInfo, chunkDelayMs: 0, maxConcurrentChunkDownloads: 1, progress: progress);

        // Assert
        Assert.Equal([1, 2, 3], progressReports);
    }

    [Fact]
    public async Task DownloadAndAssembleAsync_Legacy_EncryptedMissingToken_Throws()
    {
        // Arrange - Lines 339-341: Encrypted stream info missing token
        ConstantHandler handler = new([1, 2, 3]);
        TidalChunkDownloader downloader = new(new HttpClient(handler));

        TidalStreamInfo streamInfo = new(
            TrackId: "track-789",
            ChunkUrls: ["https://enc/1"],
            FileExtension: ".m4a",
            MimeType: "audio/mp4",
            IsEncrypted: true,
            SecurityToken: null); // Missing token

        // Act & Assert - Line 341
        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => downloader.DownloadAndAssembleAsync(streamInfo));

        Assert.Contains("Encrypted stream info missing security token", ex.Message);
    }

    #endregion

    #region DownloadAndAssembleBytesAsync Tests (Lines 382-397)

    [Fact]
    public async Task DownloadAndAssembleBytesAsync_ReturnsByteArray()
    {
        // Arrange - Lines 382-397: DownloadAndAssembleBytesAsync method
        int callCount = 0;
        byte[][] chunks = [[100, 101], [102, 103]];

        SequenceHandler handler = new(() => chunks[callCount++]);
        TidalChunkDownloader downloader = new(new HttpClient(handler));

        TidalStreamInfo streamInfo = new(
            TrackId: "track-bytes",
            ChunkUrls: ["https://bytes/1", "https://bytes/2"],
            FileExtension: ".m4a",
            MimeType: "audio/mp4",
            IsEncrypted: false,
            SecurityToken: null);

        // Act
        byte[] result = await downloader.DownloadAndAssembleBytesAsync(streamInfo);

        // Assert
        Assert.Equal([100, 101, 102, 103], result);
    }

    // Removed: DownloadAndAssembleBytesAsync_WithProgress_ReportsProgress
    // Progress<T> fires callbacks on captured SynchronizationContext, not synchronously.
    // This caused non-deterministic failures (progressReports was empty by assertion time).

    #endregion

    #region ValidateChunkAccessibilityAsync Tests (Lines 399-423)

    [Fact]
    public async Task ValidateChunkAccessibilityAsync_StringArray_EmptyUrls_ReturnsFalse()
    {
        // Arrange - Line 403-405: Empty array returns false
        ConstantHandler handler = new([1, 2, 3]);
        TidalChunkDownloader downloader = new(new HttpClient(handler));

        // Act
        bool result = await downloader.ValidateChunkAccessibilityAsync([]);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ValidateChunkAccessibilityAsync_StringArray_ValidUrl_ReturnsTrue()
    {
        // Arrange - Lines 408-409: Valid URL returns true
        ConstantHandler handler = new([1, 2, 3]);
        TidalChunkDownloader downloader = new(new HttpClient(handler));

        // Act
        bool result = await downloader.ValidateChunkAccessibilityAsync(["https://valid.test/chunk"]);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ValidateChunkAccessibilityAsync_StreamManifest_ValidUrls_ReturnsTrue()
    {
        // Arrange - Lines 420-423: ValidateChunkAccessibilityAsync with StreamManifest
        ConstantHandler handler = new([1, 2, 3]);
        TidalChunkDownloader downloader = new(new HttpClient(handler));

        // Create JsonElement for StreamManifest constructor with BTS format (direct URL)
        // BTS format uses the manifest field directly as the URL
        string json = """
            {
                "manifestMimeType": "application/vnd.tidal.bts",
                "manifest": "http://test/direct-chunk"
            }
            """;
        using System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(json);
        StreamManifest manifest = new(doc.RootElement);

        // Act
        bool result = await downloader.ValidateChunkAccessibilityAsync(manifest);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ValidateChunkAccessibilityAsync_StreamManifest_EmptyUrls_ReturnsFalse()
    {
        // Arrange - Lines 420-423 with empty chunk URLs
        ConstantHandler handler = new([1, 2, 3]);
        TidalChunkDownloader downloader = new(new HttpClient(handler));

        // Create manifest with empty chunk URLs (empty manifest field)
        string json = """
            {
                "manifestMimeType": "application/dash+xml",
                "manifest": ""
            }
            """;
        using System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(json);
        StreamManifest manifest = new(doc.RootElement);

        // Act
        bool result = await downloader.ValidateChunkAccessibilityAsync(manifest);

        // Assert
        Assert.False(result);
    }

    #endregion

    #region Cancellation Tests (Lines 39, 130, 266)

    [Fact]
    public async Task DownloadAndAssembleAsync_Cancelled_ThrowsOperationCanceledException()
    {
        // Arrange - Line 39: ThrowIfCancellationRequested
        using CancellationTokenSource cts = new();
        cts.Cancel();

        ConstantHandler handler = new([1, 2, 3]);
        TidalChunkDownloader downloader = new(new HttpClient(handler));

        TidalManifest manifest = new(
            ChunkUrls: ["https://cancel/1"],
            Codec: "flac",
            MimeType: "audio/mp4",
            FileExtension: ".flac",
            SampleRate: 44100,
            IsEncrypted: false,
            KeyId: null,
            SecurityToken: null);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => downloader.DownloadAndAssembleAsync(manifest, cancellationToken: cts.Token));
    }

    [Fact]
    public async Task DownloadAndAssembleToFileStreamAsync_Cancelled_ThrowsOperationCanceledException()
    {
        // Arrange - Line 130: ThrowIfCancellationRequested in file-backed variant
        using CancellationTokenSource cts = new();
        cts.Cancel();

        ConstantHandler handler = new([1, 2, 3]);
        TidalChunkDownloader downloader = new(new HttpClient(handler));

        TidalManifest manifest = new(
            ChunkUrls: ["https://cancel-file/1"],
            Codec: "AAC",
            MimeType: "audio/mp4",
            FileExtension: ".m4a",
            SampleRate: 44100,
            IsEncrypted: false,
            KeyId: null,
            SecurityToken: null);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => downloader.DownloadAndAssembleToFileStreamAsync(manifest, cancellationToken: cts.Token));
    }

    [Fact]
    public async Task DownloadAndAssembleAsync_Legacy_Cancelled_ThrowsOperationCanceledException()
    {
        // Arrange - Line 266: ThrowIfCancellationRequested in legacy method
        using CancellationTokenSource cts = new();
        cts.Cancel();

        ConstantHandler handler = new([1, 2, 3]);
        TidalChunkDownloader downloader = new(new HttpClient(handler));

        TidalStreamInfo streamInfo = new(
            TrackId: "track-cancel",
            ChunkUrls: ["https://cancel-legacy/1"],
            FileExtension: ".m4a",
            MimeType: "audio/mp4",
            IsEncrypted: false,
            SecurityToken: null);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => downloader.DownloadAndAssembleAsync(streamInfo, cancellationToken: cts.Token));
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

    #endregion
}
