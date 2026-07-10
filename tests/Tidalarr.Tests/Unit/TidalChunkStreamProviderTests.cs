using System.Text;
using Lidarr.Plugin.Common.Interfaces;
using Tidalarr.Core.Exceptions;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Mappers;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Streaming;
using Tidalarr.Integration;

namespace Tidalarr.Tests.Unit;

/// <summary>
/// Direct unit tests for <see cref="TidalChunkStreamProvider"/>: cancellation propagation into
/// manifest resolution, the manifest-vs-legacy path selection, the transient-failure fallback
/// contract, and the terminal-restriction (permanent unavailability) suppression contract.
/// </summary>
public class TidalChunkStreamProviderTests
{
    private const string LegacyChunkUrl = "https://cdn.example.com/legacy-chunk.m4a";
    private const string ManifestChunkUrl = "https://cdn.example.com/manifest-chunk.m4a";

    private sealed class StubCore : ITidalCore
    {
        public Func<string, TidalQuality, CancellationToken, Task<TidalPlaybackInfoDto>>? PlaybackInfo { get; set; }
        public int PlaybackCalls { get; private set; }
        public int LegacyCalls { get; private set; }
        public bool ManifestFetchCompleted { get; set; }

        public Task<TidalPlaybackInfoDto> GetPlaybackInfoAsync(string trackId, TidalQuality quality, CancellationToken cancellationToken = default)
        {
            PlaybackCalls++;
            return PlaybackInfo is null
                ? throw new NotSupportedException("No playback-info behavior configured")
                : PlaybackInfo(trackId, quality, cancellationToken);
        }

        public Task<TidalStreamInfo> GetStreamInfoAsync(string trackId, TidalQuality quality, CancellationToken cancellationToken = default)
        {
            LegacyCalls++;
            return Task.FromResult(new TidalStreamInfo(trackId, [LegacyChunkUrl], ".m4a", "audio/mp4", false, null));
        }

        public Task<TidalTrackInfo> GetTrackAsync(string trackId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new TidalTrackInfo(trackId, "Song", ["Artist"], "al1", "Album", 1, 100, TidalQuality.Lossless, true, DateTime.UtcNow));
        }

        public Task<TidalAlbumInfo> GetAlbumAsync(string albumId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new TidalAlbumInfo("", "", [], [], [], DateTime.MinValue, "", true));
        }

        public Task<List<TidalTrackInfo>> GetAlbumTracksAsync(string albumId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new List<TidalTrackInfo>());
        }

        public Task<TidalAlbumInfo> GetAlbumWithTracksAsync(string albumId, CancellationToken cancellationToken = default)
        {
            return GetAlbumAsync(albumId, cancellationToken);
        }

        public Task<TidalSearchResults> SearchAsync(string query, int limit = 100, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new TidalSearchResults([], [], [], 0, false));
        }

        public Task<bool> IsAuthenticatedAsync()
        {
            return Task.FromResult(true);
        }
    }

    private sealed class RecordingHandler(byte[] payload) : HttpMessageHandler
    {
        private readonly byte[] _payload = payload;
        public List<string> RequestedUrls { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            lock (RequestedUrls)
            {
                RequestedUrls.Add(request.RequestUri?.ToString() ?? string.Empty);
            }
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new ByteArrayContent(this._payload) });
        }
    }

    private static TidalPlaybackInfoDto BtsPlaybackInfo(params string[] chunkUrls)
    {
        string urls = string.Join(",", chunkUrls.Select(u => $"\"{u}\""));
        string bts = $$"""{"urls":[{{urls}}],"codecs":"mp4a.40.2","mimeType":"audio/mp4","encryptionType":"NONE"}""";
        return new TidalPlaybackInfoDto(
            manifest: Convert.ToBase64String(Encoding.UTF8.GetBytes(bts)),
            manifestMimeType: "application/vnd.tidal.bts",
            encryptionType: "NONE",
            securityToken: null);
    }

    /// <summary>DASH manifest whose empty SegmentTimeline yields zero media segments (no chunk URLs).</summary>
    private static TidalPlaybackInfoDto ChunklessDashPlaybackInfo()
    {
        const string dash = """<?xml version="1.0"?><MPD><Period><AdaptationSet codecs="mp4a.40.2"><SegmentTemplate media="https://cdn.example.com/seg$Number$.m4a"><SegmentTimeline></SegmentTimeline></SegmentTemplate></AdaptationSet></Period></MPD>""";
        return new TidalPlaybackInfoDto(
            manifest: Convert.ToBase64String(Encoding.UTF8.GetBytes(dash)),
            manifestMimeType: "application/dash+xml",
            encryptionType: "NONE",
            securityToken: null);
    }

    private static (TidalChunkStreamProvider Provider, StubCore Core, RecordingHandler Handler) Build(byte[]? payload = null)
    {
        StubCore core = new();
        RecordingHandler handler = new(payload ?? [0x01, 0x02, 0x03, 0x04]);
        TidalStreamService streamService = new(core, new TidalManifestParser());
        TidalChunkDownloader downloader = new(new HttpClient(handler), segmentPolicy: TidalTestPolicies.Resolving);
        TidalDownloadClientSettings settings = new() { PreferredQuality = TidalQuality.Lossless, DownloadPath = Path.GetTempPath() };
        TidalChunkStreamProvider provider = new(streamService, downloader, new TidalModelMapper(), settings);
        return (provider, core, handler);
    }

    [Fact]
    public async Task GetStreamAsync_TokenCancelledBeforeManifestResolution_ThrowsOperationCanceled_WithoutDownloading()
    {
        (TidalChunkStreamProvider provider, StubCore core, RecordingHandler handler) = Build();
        core.PlaybackInfo = (trackId, quality, ct) =>
        {
            // A real API client observes the caller's token; the provider must forward it.
            ct.ThrowIfCancellationRequested();
            core.ManifestFetchCompleted = true;
            return Task.FromResult(BtsPlaybackInfo(ManifestChunkUrl));
        };
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.GetStreamAsync("t1", quality: null, cts.Token));

        // The manifest fetch must be aborted by the forwarded token, not allowed to complete
        // with the cancellation only observed later by the chunk downloader.
        Assert.False(core.ManifestFetchCompleted, "cancellation token was not forwarded into manifest resolution");
        Assert.Equal(0, core.LegacyCalls);
        Assert.Empty(handler.RequestedUrls);
    }

    [Fact]
    public async Task GetStreamAsync_CancellationDuringManifestResolution_PropagatesAndNeverFallsBackToLegacy()
    {
        (TidalChunkStreamProvider provider, StubCore core, RecordingHandler handler) = Build();
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();
        core.PlaybackInfo = (_, _, _) => throw new OperationCanceledException(cts.Token);

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.GetStreamAsync("t1", quality: null, cts.Token));

        // Cancellation must not be treated as a transient manifest failure: the legacy
        // stream-info path (a second API call) must never run.
        Assert.Equal(0, core.LegacyCalls);
        Assert.Empty(handler.RequestedUrls);
    }

    [Fact]
    public async Task GetStreamAsync_PermanentRestriction_RecordsToTerminalScope_AndRethrows()
    {
        (TidalChunkStreamProvider provider, StubCore core, RecordingHandler handler) = Build();
        core.PlaybackInfo = (trackId, quality, _) =>
            throw new TidalStreamUnavailableException(trackId, quality, "rights removed", TidalStreamUnavailableReason.RightsRemoved);

        using IDisposable scope = TidalTerminalRestrictionScope.Begin();
        TidalStreamUnavailableException ex = await Assert.ThrowsAsync<TidalStreamUnavailableException>(
            () => provider.GetStreamAsync("t-perm", quality: null, CancellationToken.None));

        Assert.Equal(TidalStreamUnavailableReason.RightsRemoved, ex.Reason);
        TidalTerminalRestriction restriction = Assert.Single(TidalTerminalRestrictionScope.Snapshot());
        Assert.Equal("t-perm", restriction.TrackId);
        Assert.Equal(TidalStreamUnavailableReason.RightsRemoved, restriction.Reason);
        // A permanent restriction must NOT be hidden by the legacy fallback.
        Assert.Equal(0, core.LegacyCalls);
        Assert.Empty(handler.RequestedUrls);
    }

    [Fact]
    public async Task GetStreamAsync_ManifestWithChunks_UsesChunkPath_NotLegacy()
    {
        byte[] payload = [0x00, 0x00, 0x00, 0x00, (byte)'f', (byte)'t', (byte)'y', (byte)'p'];
        (TidalChunkStreamProvider provider, StubCore core, RecordingHandler handler) = Build(payload);
        core.PlaybackInfo = (_, _, _) => Task.FromResult(BtsPlaybackInfo(ManifestChunkUrl));

        AudioStreamResult result = await provider.GetStreamAsync("t1", quality: null, CancellationToken.None);

        Assert.Equal("m4a", result.SuggestedExtension);
        using MemoryStream buffer = new();
        await result.Stream.CopyToAsync(buffer);
        Assert.Equal(payload, buffer.ToArray());
        Assert.Contains(ManifestChunkUrl, handler.RequestedUrls);
        Assert.Equal(0, core.LegacyCalls);
    }

    [Fact]
    public async Task GetStreamAsync_ManifestWithoutChunks_FallsBackToLegacyStreamInfo()
    {
        (TidalChunkStreamProvider provider, StubCore core, RecordingHandler handler) = Build();
        core.PlaybackInfo = (_, _, _) => Task.FromResult(ChunklessDashPlaybackInfo());

        AudioStreamResult result = await provider.GetStreamAsync("t1", quality: null, CancellationToken.None);

        Assert.NotNull(result.Stream);
        Assert.Equal(1, core.LegacyCalls);
        Assert.Contains(LegacyChunkUrl, handler.RequestedUrls);
        Assert.DoesNotContain(handler.RequestedUrls, u => u.Contains("seg", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GetStreamAsync_TransientManifestFailure_FallsBackToLegacyExactlyOnce()
    {
        (TidalChunkStreamProvider provider, StubCore core, RecordingHandler handler) = Build();
        core.PlaybackInfo = (_, _, _) => throw new HttpRequestException("HTTP 503 from playback-info");

        AudioStreamResult result = await provider.GetStreamAsync("t1", quality: null, CancellationToken.None);

        Assert.NotNull(result.Stream);
        Assert.Equal(1, core.PlaybackCalls);
        Assert.Equal(1, core.LegacyCalls);
        Assert.Equal([LegacyChunkUrl], handler.RequestedUrls);
    }
}
