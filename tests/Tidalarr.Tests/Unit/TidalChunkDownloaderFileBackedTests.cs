using System.Net;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Streaming;

namespace Tidalarr.Tests.Unit;

public class TidalChunkDownloaderFileBackedTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> handler) : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> handler = handler ?? throw new ArgumentNullException(nameof(handler));

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(this.handler(request, cancellationToken));
        }
    }

    [Fact]
    public async Task DownloadAndAssembleToFileStreamAsync_ShouldReturn_FileBackedStream_AndConcatenateChunks()
    {
        string[] urls =
        [
            "http://example.test/chunk/1",
            "http://example.test/chunk/2"
        ];

        Dictionary<string, byte[]> chunkMap = new()
        {
            [urls[0]] = [1, 2, 3],
            [urls[1]] = [4, 5]
        };

        StubHandler handler = new((req, _) =>
        {
            string url = req.RequestUri?.ToString() ?? string.Empty;
            return !chunkMap.TryGetValue(url, out byte[]? payload)
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(payload)
                };
        });

        using HttpClient httpClient = new(handler);
        TidalChunkDownloader downloader = new(httpClient, segmentPolicy: TidalTestPolicies.Resolving);

        TidalManifest manifest = new(
            ChunkUrls: urls,
            Codec: "AAC",
            MimeType: "audio/mp4",
            FileExtension: "m4a",
            SampleRate: 44100,
            IsEncrypted: false,
            KeyId: null,
            SecurityToken: null);

        string? filePath = null;
        await using (Stream stream = await downloader.DownloadAndAssembleToFileStreamAsync(manifest, chunkDelayMs: 0))
        {
            FileStream fileStream = Assert.IsType<FileStream>(stream);
            filePath = fileStream.Name;
            Assert.True(File.Exists(filePath));

            using MemoryStream ms = new();
            await stream.CopyToAsync(ms);
            Assert.Equal([1, 2, 3, 4, 5], ms.ToArray());
        }

        Assert.False(string.IsNullOrWhiteSpace(filePath));
        Assert.False(File.Exists(filePath!));
    }

    [Fact]
    public async Task DownloadAndAssembleToFileStreamAsync_WithParallelChunks_PreservesChunkOrder()
    {
        string[] urls =
        [
            "http://example.test/chunk/1",
            "http://example.test/chunk/2",
            "http://example.test/chunk/3"
        ];

        Dictionary<string, (byte[] payload, int delayMs)> chunkMap = new()
        {
            [urls[0]] = ([1], 150),
            [urls[1]] = ([2], 0),
            [urls[2]] = ([3], 50)
        };

        DelayedHandler handler = new(async (req, ct) =>
        {
            string url = req.RequestUri?.ToString() ?? string.Empty;
            if (!chunkMap.TryGetValue(url, out (byte[] payload, int delayMs) entry))
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            if (entry.delayMs > 0)
            {
                await Task.Delay(entry.delayMs, ct);
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(entry.payload)
            };
        });

        using HttpClient httpClient = new(handler);
        TidalChunkDownloader downloader = new(httpClient, segmentPolicy: TidalTestPolicies.Resolving);

        TidalManifest manifest = new(
            ChunkUrls: urls,
            Codec: "AAC",
            MimeType: "audio/mp4",
            FileExtension: "m4a",
            SampleRate: 44100,
            IsEncrypted: false,
            KeyId: null,
            SecurityToken: null);

        await using Stream stream = await downloader.DownloadAndAssembleToFileStreamAsync(manifest, chunkDelayMs: 0, maxConcurrentChunkDownloads: 3);
        using MemoryStream ms = new();
        await stream.CopyToAsync(ms);
        Assert.Equal([1, 2, 3], ms.ToArray());
    }

    private sealed class DelayedHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler = handler ?? throw new ArgumentNullException(nameof(handler));

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return this.handler(request, cancellationToken);
        }
    }
}
