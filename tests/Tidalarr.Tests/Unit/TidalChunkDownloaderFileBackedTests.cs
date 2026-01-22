using System.Net;
using System.Net.Http;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Streaming;

namespace Tidalarr.Tests.Unit;

public class TidalChunkDownloaderFileBackedTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> handler;

        public StubHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> handler)
        {
            this.handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(this.handler(request, cancellationToken));
        }
    }

    [Fact]
    public async Task DownloadAndAssembleToFileStreamAsync_ShouldReturn_FileBackedStream_AndConcatenateChunks()
    {
        var chunkMap = new Dictionary<string, byte[]>
        {
            ["http://example.test/chunk/1"] = [1, 2, 3],
            ["http://example.test/chunk/2"] = [4, 5]
        };

        var handler = new StubHandler((req, _) =>
        {
            var url = req.RequestUri?.ToString() ?? string.Empty;
            if (!chunkMap.TryGetValue(url, out var payload))
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(payload)
            };
        });

        using var httpClient = new HttpClient(handler);
        var downloader = new TidalChunkDownloader(httpClient);

        var manifest = new TidalManifest(
            ChunkUrls: [.. chunkMap.Keys],
            Codec: "AAC",
            MimeType: "audio/mp4",
            FileExtension: "m4a",
            SampleRate: 44100,
            IsEncrypted: false,
            KeyId: null,
            SecurityToken: null);

        string? filePath = null;
        await using (var stream = await downloader.DownloadAndAssembleToFileStreamAsync(manifest, chunkDelayMs: 0))
        {
            var fileStream = Assert.IsType<FileStream>(stream);
            filePath = fileStream.Name;
            Assert.True(File.Exists(filePath));

            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            Assert.Equal([1, 2, 3, 4, 5], ms.ToArray());
        }

        Assert.False(string.IsNullOrWhiteSpace(filePath));
        Assert.False(File.Exists(filePath!));
    }
}

