using System;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Tidalarr.Domain.Streaming;
using Xunit;

namespace Tidalarr.Tests;

public class TidalChunkDownloaderRetryTests
{
    private class FlakyHandler : HttpMessageHandler
    {
        private readonly int _failures;
        private int _count;
        private readonly byte[] _payload;
        public int Attempts => _count;
        public FlakyHandler(int failures, byte[] payload)
        { _failures = failures; _payload = payload; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _count);
            if (_count <= _failures)
                throw new HttpRequestException("flaky");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(_payload) });
        }
    }

    [Fact]
    public async Task DownloadChunkWithRetryAsync_EventuallySucceeds()
    {
        var handler = new FlakyHandler(failures: 2, payload: new byte[] { 1, 2, 3 });
        var dl = new TidalChunkDownloader(new HttpClient(handler));

        var mi = typeof(TidalChunkDownloader).GetMethod("DownloadChunkWithRetryAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(mi);
        var task = (Task<byte[]>)mi!.Invoke(dl, new object[] { "https://chunk", 3 })!;
        var result = await task;

        Assert.Equal(3, handler.Attempts);
        Assert.Equal(new byte[] { 1, 2, 3 }, result);
    }

    [Fact]
    public async Task DownloadChunkWithRetryAsync_ExhaustsRetries_ThrowsHttpRequest()
    {
        var handler = new FlakyHandler(failures: 3, payload: Array.Empty<byte>());
        var dl = new TidalChunkDownloader(new HttpClient(handler));
        var mi = typeof(TidalChunkDownloader).GetMethod("DownloadChunkWithRetryAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(mi);
        var task = (Task<byte[]>)mi!.Invoke(dl, new object[] { "https://chunk", 3 })!;
        await Assert.ThrowsAsync<HttpRequestException>(async () => await task);
        Assert.Equal(3, handler.Attempts);
    }
}



