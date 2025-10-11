using Lidarr.Plugin.Common.Services.Intelligence;
using Tidalarr.Application.Services;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Quality;
using Xunit;

namespace Tidalarr.Tests;

public class TidalSearchServiceOptimizerTests
{
    private class CoreFake : ITidalCore
    {
        public Task<TidalTrackInfo> GetTrackAsync(string trackId, CancellationToken cancellationToken = default) => Task.FromResult(new TidalTrackInfo("", "", new(), "", "", 0, 0, TidalQuality.High, true, DateTime.MinValue));
        public Task<TidalAlbumInfo> GetAlbumAsync(string albumId, CancellationToken cancellationToken = default) => Task.FromResult(new TidalAlbumInfo("", "", new(), new(), new(), DateTime.MinValue, "", true));
        public Task<List<TidalTrackInfo>> GetAlbumTracksAsync(string albumId, CancellationToken cancellationToken = default) => Task.FromResult(new List<TidalTrackInfo>());
        public Task<TidalAlbumInfo> GetAlbumWithTracksAsync(string albumId, CancellationToken cancellationToken = default) => GetAlbumAsync(albumId, cancellationToken);
        public Task<TidalSearchResults> SearchAsync(string query, int limit = 100, CancellationToken cancellationToken = default)
        {
            var album = new TidalAlbumInfo("al1", "Album", new() { "Artist" }, new(), new() { TidalQuality.Lossless }, DateTime.UtcNow, "c", true);
            return Task.FromResult(new TidalSearchResults(new() { album }, new(), 1, false));
        }
        public Task<TidalStreamInfo> GetStreamInfoAsync(string trackId, TidalQuality quality, CancellationToken cancellationToken = default) => Task.FromResult(new TidalStreamInfo(trackId, Array.Empty<string>(), ".flac", "audio/flac", false, null));
        public Task<bool> IsAuthenticatedAsync() => Task.FromResult(true);
    }

    private class OptimizerStub : IQueryOptimizer
    {
        public List<string> Learned = new();
        public Task<OptimizedQuery> OptimizeQueryAsync(string originalQuery, QueryContext? context = null)
            => Task.FromResult(new OptimizedQuery { Query = originalQuery + " optimized", Confidence = 0.9 });
        public Task LearnFromResultsAsync(string query, QueryResults results, QueryFeedback userFeedback)
        { Learned.Add(query); return Task.CompletedTask; }
        public Task<OptimizationMetrics> GetMetricsAsync() => Task.FromResult(new OptimizationMetrics());
        public Task ResetAsync() => Task.CompletedTask;
    }

    [Fact]
    public async Task SearchWithQualityDetection_UsesOptimizer_AndLearns()
    {
        var optimizer = new OptimizerStub();
        var svc = new TidalSearchService(new CoreFake(), new TidalQualityDetector(), optimizer);
        var results = await svc.SearchWithQualityDetectionAsync("query", TidalQuality.Lossless);
        Assert.NotEmpty(results.Albums);
        // learning happens fire-and-forget; wait briefly
        for (int i = 0; i < 10 && optimizer.Learned.Count == 0; i++)
            await Task.Delay(20);
        Assert.True(optimizer.Learned.Count > 0);
        Assert.Contains("query optimized", optimizer.Learned[0]);
    }
}



