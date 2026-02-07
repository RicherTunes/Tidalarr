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
        private readonly TaskCompletionSource _learnCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<string> Learned = [];
        public Task<OptimizedQuery> OptimizeQueryAsync(string originalQuery, QueryContext? context = null)
        {
            return Task.FromResult(new OptimizedQuery { Query = originalQuery + " optimized", Confidence = 0.9 });
        }

        public Task LearnFromResultsAsync(string query, QueryResults results, QueryFeedback userFeedback)
        {
            this.Learned.Add(query);
            this._learnCompleted.TrySetResult();
            return Task.CompletedTask;
        }

        /// <summary>
        /// Waits for the fire-and-forget learning callback to complete,
        /// replacing brittle Task.Delay-based polling.
        /// </summary>
        public Task WaitForLearnAsync(TimeSpan timeout)
        {
            return Task.WhenAny(this._learnCompleted.Task, Task.Delay(timeout));
        }

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
        // Learning happens fire-and-forget via Task.Run(); use a
        // TaskCompletionSource-based waiter instead of brittle Task.Delay polling.
        await optimizer.WaitForLearnAsync(TimeSpan.FromSeconds(5));

        Assert.True(optimizer.Learned.Count > 0);
        Assert.Contains("query optimized", optimizer.Learned[0]);
    }
}



