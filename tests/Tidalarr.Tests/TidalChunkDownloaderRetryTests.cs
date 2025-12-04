using System.Net;
using System.Reflection;
using Tidalarr.Domain.Streaming;

namespace Tidalarr.Tests;

public class TidalChunkDownloaderRetryTests
{
    private class FlakyHandler(int failures, byte[] payload) : HttpMessageHandler
    {
        private readonly int _failures = failures;
        private int _count;
        private readonly byte[] _payload = payload;
        public int Attempts => this._count;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _ = Interlocked.Increment(ref this._count);
            return this._count <= this._failures
                ? throw new HttpRequestException("flaky")
                : Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(this._payload) });
        }
    }

    [Fact]
    public async Task DownloadChunkWithRetryAsync_EventuallySucceeds()
    {
        FlakyHandler handler = new FlakyHandler(failures: 2, payload: [1, 2, 3]);
        TidalChunkDownloader dl = new TidalChunkDownloader(new HttpClient(handler));

        MethodInfo? mi = typeof(TidalChunkDownloader).GetMethod("DownloadChunkWithRetryAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(mi);
        Task<byte[]> task = (Task<byte[]>)mi!.Invoke(dl, ["https://chunk", 3])!;
        byte[] result = await task;

        Assert.Equal(3, handler.Attempts);
        Assert.Equal(new byte[] { 1, 2, 3 }, result);
    }

    [Fact]
    public async Task DownloadChunkWithRetryAsync_ExhaustsRetries_ThrowsHttpRequest()
    {
        FlakyHandler handler = new FlakyHandler(failures: 3, payload: []);
        TidalChunkDownloader dl = new TidalChunkDownloader(new HttpClient(handler));
        MethodInfo? mi = typeof(TidalChunkDownloader).GetMethod("DownloadChunkWithRetryAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(mi);
        Task<byte[]> task = (Task<byte[]>)mi!.Invoke(dl, ["https://chunk", 3])!;
        _ = await Assert.ThrowsAsync<HttpRequestException>(async () => await task);
        Assert.Equal(3, handler.Attempts);
    }
}



