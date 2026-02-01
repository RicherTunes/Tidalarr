using Lidarr.Plugin.Common.Services.Intelligence;
using Tidalarr.Application.Services;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Quality;

namespace Tidalarr.Tests;

public class TidalSearchServiceOptimizerTests
{
    private class CoreFake : ITidalCore
    {
        public Task<TidalTrackInfo> GetTrackAsync(string trackId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new TidalTrackInfo("", "", [], "", "", 0, 0, TidalQuality.High, true, DateTime.MinValue));
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
            TidalAlbumInfo album = new("al1", "Album", ["Artist"], [], [TidalQuality.Lossless], DateTime.UtcNow, "c", true);
            return Task.FromResult(new TidalSearchResults([album], [], [], 1, false));
        }
        public Task<TidalStreamInfo> GetStreamInfoAsync(string trackId, TidalQuality quality, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new TidalStreamInfo(trackId, [], ".flac", "audio/flac", false, null));
        }

        public Task<bool> IsAuthenticatedAsync()
        {
            return Task.FromResult(true);
        }
    }

    private class OptimizerStub : IQueryOptimizer
    {
        public List<string> Learned = [];
        public Task<OptimizedQuery> OptimizeQueryAsync(string originalQuery, QueryContext? context = null)
        {
            return Task.FromResult(new OptimizedQuery { Query = originalQuery + " optimized", Confidence = 0.9 });
        }

        public Task LearnFromResultsAsync(string query, QueryResults results, QueryFeedback userFeedback)
        { this.Learned.Add(query); return Task.CompletedTask; }
        public Task<OptimizationMetrics> GetMetricsAsync()
        {
            return Task.FromResult(new OptimizationMetrics());
        }

        public Task ResetAsync()
        {
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task SearchWithQualityDetection_UsesOptimizer_AndLearns()
    {
        OptimizerStub optimizer = new();
        TidalSearchService svc = new(new CoreFake(), new TidalQualityDetector(), optimizer);
        TidalSearchResults results = await svc.SearchWithQualityDetectionAsync("query", TidalQuality.Lossless);
        Assert.NotEmpty(results.Albums);
        // learning happens fire-and-forget; wait briefly
        for (int i = 0; i < 10 && optimizer.Learned.Count == 0; i++)
        {
            await Task.Delay(20);
        }

        Assert.True(optimizer.Learned.Count > 0);
        Assert.Contains("query optimized", optimizer.Learned[0]);
    }
}



