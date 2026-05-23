using Microsoft.Extensions.Logging;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Streaming;
using Tidalarr.Integration;

using Xunit;

namespace Tidalarr.Tests;

public sealed class TidalDownloadClientQualityDowngradeTests
{
    private sealed class CapturingLogger : ILogger
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = new();
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Entries.Add((logLevel, formatter(state, exception)));
        private sealed class NullScope : IDisposable { public static readonly NullScope Instance = new(); public void Dispose() { } }
    }

    private sealed class CoreStubReturningDelivered(TidalQuality delivered) : ITidalCore
    {
        public Task<TidalTrackInfo> GetTrackAsync(string trackId, CancellationToken ct = default) =>
            Task.FromResult(new TidalTrackInfo(trackId, "Song", ["Artist"], "al1", "Album", 1, 100, TidalQuality.High, true, DateTime.UtcNow));
        public Task<TidalAlbumInfo> GetAlbumAsync(string albumId, CancellationToken ct = default) =>
            Task.FromResult(new TidalAlbumInfo("", "", [], [], [], DateTime.MinValue, "", true));
        public Task<List<TidalTrackInfo>> GetAlbumTracksAsync(string albumId, CancellationToken ct = default) =>
            Task.FromResult(new List<TidalTrackInfo>());
        public Task<TidalAlbumInfo> GetAlbumWithTracksAsync(string albumId, CancellationToken ct = default) => GetAlbumAsync(albumId, ct);
        public Task<TidalSearchResults> SearchAsync(string query, int limit = 100, CancellationToken ct = default) =>
            Task.FromResult(new TidalSearchResults([], [], [], 0, false));
        public Task<TidalStreamInfo> GetStreamInfoAsync(string trackId, TidalQuality quality, CancellationToken ct = default) =>
            Task.FromResult(new TidalStreamInfo(trackId, ["https://chunk-1"], ".flac", "audio/flac", false, null, delivered));
        public Task<bool> IsAuthenticatedAsync() => Task.FromResult(true);
    }

    private sealed class ExposedDownloadClient(TidalStreamService s, TidalChunkDownloader cd, ITidalCore c, Domain.Quality.TidalQualityDetector qd, TidalDownloadClientSettings st, ILogger l)
        : TidalDownloadClient(s, cd, c, qd, st, l)
    {
        public Task<string> ExposeGetStreamUrlAsync(string trackId, string quality) => base.GetStreamUrlAsync(trackId, quality);
    }

    [Fact]
    public async Task GetStreamUrl_LogsWarning_WhenTidalDeliversBelowPreferredQuality()
    {
        var logger = new CapturingLogger();
        var settings = new TidalDownloadClientSettings { PreferredQuality = TidalQuality.HiRes, DownloadPath = Path.GetTempPath() };
        var core = new CoreStubReturningDelivered(TidalQuality.Lossless);
        var streamSvc = new TidalStreamService(core, new TidalManifestParser());
        var client = new ExposedDownloadClient(streamSvc, new TidalChunkDownloader(new HttpClient()), core, new Domain.Quality.TidalQualityDetector(), settings, logger);

        _ = await client.ExposeGetStreamUrlAsync("t1", "HI_RES");

        var warning = logger.Entries.FirstOrDefault(e => e.Level == LogLevel.Warning);
        Assert.NotEqual(default, warning);
        Assert.Contains("tidal.com/plans", warning.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("HiRes", warning.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Lossless", warning.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetStreamUrl_DoesNotWarn_WhenTidalDeliversAtRequestedQuality()
    {
        var logger = new CapturingLogger();
        var settings = new TidalDownloadClientSettings { PreferredQuality = TidalQuality.Lossless, DownloadPath = Path.GetTempPath() };
        var core = new CoreStubReturningDelivered(TidalQuality.Lossless);
        var streamSvc = new TidalStreamService(core, new TidalManifestParser());
        var client = new ExposedDownloadClient(streamSvc, new TidalChunkDownloader(new HttpClient()), core, new Domain.Quality.TidalQualityDetector(), settings, logger);

        _ = await client.ExposeGetStreamUrlAsync("t1", "LOSSLESS");

        Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task GetStreamUrl_DoesNotWarn_WhenDeliveredQualityUnknown()
    {
        var logger = new CapturingLogger();
        var settings = new TidalDownloadClientSettings { PreferredQuality = TidalQuality.HiRes, DownloadPath = Path.GetTempPath() };
        // DeliveredQuality null → API didn't tell us; don't manufacture a warning.
        var streamSvc = new TidalStreamService(new CoreStubReturningDelivered(TidalQuality.Lossless), new TidalManifestParser());
        var coreNoDelivered = new NoDeliveredQualityCore();
        var streamSvc2 = new TidalStreamService(coreNoDelivered, new TidalManifestParser());
        var client = new ExposedDownloadClient(streamSvc2, new TidalChunkDownloader(new HttpClient()), coreNoDelivered, new Domain.Quality.TidalQualityDetector(), settings, logger);

        _ = await client.ExposeGetStreamUrlAsync("t1", "HI_RES");

        Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Warning);
    }

    private sealed class NoDeliveredQualityCore : ITidalCore
    {
        public Task<TidalTrackInfo> GetTrackAsync(string trackId, CancellationToken ct = default) =>
            Task.FromResult(new TidalTrackInfo(trackId, "Song", ["Artist"], "al1", "Album", 1, 100, TidalQuality.High, true, DateTime.UtcNow));
        public Task<TidalAlbumInfo> GetAlbumAsync(string albumId, CancellationToken ct = default) =>
            Task.FromResult(new TidalAlbumInfo("", "", [], [], [], DateTime.MinValue, "", true));
        public Task<List<TidalTrackInfo>> GetAlbumTracksAsync(string albumId, CancellationToken ct = default) =>
            Task.FromResult(new List<TidalTrackInfo>());
        public Task<TidalAlbumInfo> GetAlbumWithTracksAsync(string albumId, CancellationToken ct = default) => GetAlbumAsync(albumId, ct);
        public Task<TidalSearchResults> SearchAsync(string query, int limit = 100, CancellationToken ct = default) =>
            Task.FromResult(new TidalSearchResults([], [], [], 0, false));
        public Task<TidalStreamInfo> GetStreamInfoAsync(string trackId, TidalQuality quality, CancellationToken ct = default) =>
            Task.FromResult(new TidalStreamInfo(trackId, ["https://chunk-1"], ".flac", "audio/flac", false, null, null));
        public Task<bool> IsAuthenticatedAsync() => Task.FromResult(true);
    }
}
