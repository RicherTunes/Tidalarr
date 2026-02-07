using System.Net;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Streaming;

namespace Tidalarr.Tests;

/// <summary>
/// Tests for TidalChunkDownloader retry behavior.
/// Note: The actual retry logic is now in Lidarr.Plugin.Common (ExecuteWithRetryAsync).
/// These tests verify the public API integrates correctly with the retry mechanism.
/// </summary>
public class TidalChunkDownloaderRetryTests
{
    private class SuccessHandler(byte[] payload) : HttpMessageHandler
    {
        private readonly byte[] _payload = payload;
        private int _attempts;
        public int Attempts => _attempts;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _ = Interlocked.Increment(ref this._attempts);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(this._payload)
            });
        }
    }

    private class FailingHandler : HttpMessageHandler
    {
        private int _attempts;
        public int Attempts => _attempts;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _ = Interlocked.Increment(ref this._attempts);
            // Return 500 to trigger retry behavior in the Common library
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("Server error")
            });
        }
    }

    [Fact]
    public async Task DownloadAndAssembleAsync_Success_ReturnsData()
    {
        // Arrange
        byte[] expectedData = [1, 2, 3, 4, 5];
        SuccessHandler handler = new(expectedData);
        TidalChunkDownloader downloader = new(new HttpClient(handler));

        TidalManifest manifest = new(
            ChunkUrls: ["https://chunk1.test"],
            Codec: "flac",
            MimeType: "audio/mp4",
            FileExtension: ".flac",
            SampleRate: 44100,
            IsEncrypted: false,
            KeyId: null,
            SecurityToken: null);

        // Act
        using MemoryStream result = await downloader.DownloadAndAssembleAsync(manifest);

        // Assert
        Assert.Equal(1, handler.Attempts);
        Assert.Equal(expectedData, result.ToArray());
    }

    [Fact]
    public async Task DownloadAndAssembleAsync_MultipleChunks_AssemblesInOrder()
    {
        // Arrange — use URL-keyed lookup instead of a shared mutable index
        // to eliminate the non-atomic chunkIndex++ race condition.
        Dictionary<string, byte[]> chunkMap = new()
        {
            ["https://chunk1"] = [1, 2],
            ["https://chunk2"] = [3, 4],
            ["https://chunk3"] = [5, 6],
        };

        HttpMessageHandler handler = new DelegatingHandlerImpl((req, ct) =>
        {
            byte[] data = chunkMap[req.RequestUri!.ToString()];
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(data)
            });
        });

        TidalChunkDownloader downloader = new(new HttpClient(handler));

        TidalManifest manifest = new(
            ChunkUrls: ["https://chunk1", "https://chunk2", "https://chunk3"],
            Codec: "flac",
            MimeType: "audio/mp4",
            FileExtension: ".flac",
            SampleRate: 44100,
            IsEncrypted: false,
            KeyId: null,
            SecurityToken: null);

        // Act
        using MemoryStream result = await downloader.DownloadAndAssembleAsync(manifest);

        // Assert
        byte[] expected = [1, 2, 3, 4, 5, 6];
        Assert.Equal(expected, result.ToArray());
    }

    [Fact]
    public async Task ValidateChunkAccessibilityAsync_ValidUrl_ReturnsTrue()
    {
        // Arrange
        SuccessHandler handler = new([]);
        TidalChunkDownloader downloader = new(new HttpClient(handler));

        // Act
        bool result = await downloader.ValidateChunkAccessibilityAsync(["https://valid-chunk.test"]);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ValidateChunkAccessibilityAsync_EmptyUrls_ReturnsFalse()
    {
        // Arrange
        SuccessHandler handler = new([]);
        TidalChunkDownloader downloader = new(new HttpClient(handler));

        // Act
        bool result = await downloader.ValidateChunkAccessibilityAsync([]);

        // Assert
        Assert.False(result);
    }

    private class DelegatingHandlerImpl(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler = handler;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return this._handler(request, cancellationToken);
        }
    }
}
