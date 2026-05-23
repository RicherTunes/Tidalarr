using Lidarr.Plugin.Abstractions.Contracts;
using Lidarr.Plugin.Common.Services.Bridge;

using Microsoft.Extensions.Logging.Abstractions;

using Tidalarr.Application.Services;
using Tidalarr.Core.Exceptions;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Integration;

namespace Tidalarr.Tests;

/// <summary>
/// Tests for the AuthFailureGate wire-up on TidalIndexer. Closes the gap
/// flagged in the cross-plugin auth-thrash audit (2026-05-11): Tidalarr
/// had IAuthFailureHandler wired in AuthenticateAsync but the search
/// methods did not consult it. The gate now (a) short-circuits searches
/// to empty when auth is latched bad and probe slot is exhausted, and
/// (b) latches the gate from 401/403 thrown by the underlying core.
/// </summary>
public sealed class TidalIndexerAuthGateTests
{
    private sealed class CountingCore : ITidalCore
    {
        public int SearchCalls { get; private set; }
        public int AlbumCalls { get; private set; }
        public Func<Exception>? ThrowFactory { get; set; }
        public bool Authenticated { get; set; } = true;

        public Task<TidalSearchResults> SearchAsync(string query, int limit = 100, CancellationToken cancellationToken = default)
        {
            this.SearchCalls++;
            if (this.ThrowFactory is not null) throw this.ThrowFactory();
            return Task.FromResult(new TidalSearchResults([], [], [], 0, false));
        }
        public Task<TidalAlbumInfo> GetAlbumAsync(string albumId, CancellationToken cancellationToken = default)
        {
            this.AlbumCalls++;
            if (this.ThrowFactory is not null) throw this.ThrowFactory();
            return Task.FromResult(new TidalAlbumInfo("", "", [], [], [], DateTime.MinValue, "", true));
        }
        public Task<TidalTrackInfo> GetTrackAsync(string trackId, CancellationToken cancellationToken = default)
            => Task.FromResult(new TidalTrackInfo("", "", [], "", "", 0, 0, TidalQuality.High, true, DateTime.MinValue));
        public Task<List<TidalTrackInfo>> GetAlbumTracksAsync(string albumId, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<TidalTrackInfo>());
        public Task<TidalAlbumInfo> GetAlbumWithTracksAsync(string albumId, CancellationToken cancellationToken = default)
            => this.GetAlbumAsync(albumId, cancellationToken);
        public Task<TidalStreamInfo> GetStreamInfoAsync(string trackId, TidalQuality quality, CancellationToken cancellationToken = default)
            => Task.FromResult(new TidalStreamInfo(trackId, [], ".flac", "audio/flac", false, null));
        public Task<bool> IsAuthenticatedAsync() => Task.FromResult(this.Authenticated);
    }

    private static (TidalIndexer Indexer, CountingCore Core, DefaultAuthFailureHandler Handler, AuthFailureGate Gate)
        CreateIndexer()
    {
        var core = new CountingCore();
        var handler = new DefaultAuthFailureHandler(Microsoft.Extensions.Logging.Abstractions.NullLogger<DefaultAuthFailureHandler>.Instance);
        // Use a 5-minute probe interval so we never accidentally roll over inside a test run.
        var gate = new AuthFailureGate(handler, TimeProvider.System, TimeSpan.FromMinutes(5));
        var settings = new TidalIndexerSettings { TidalMarket = "US", RedirectUrl = "https://tidal.com/android/login/auth?code=x&state=y", ConfigPath = Path.GetTempPath() };
        var indexer = new TidalIndexer(
            new TidalSearchService(core, new Tidalarr.Domain.Quality.TidalQualityDetector()),
            core,
            settings,
            NullLogger.Instance,
            tokenProvider: null,
            authHandler: handler,
            statusReporter: null,
            authGate: gate);
        return (indexer, core, handler, gate);
    }

    [Fact]
    public async Task SearchAlbums_AuthLatchedBad_ShortCircuitsAfterFirstProbe()
    {
        var (indexer, core, handler, _) = CreateIndexer();
        await handler.HandleFailureAsync(new AuthFailure { ErrorCode = "401", Message = "token expired" });

        var first = await indexer.SearchAlbumsInternalAsync("query", CancellationToken.None);
        Assert.Empty(first);
        Assert.Equal(1, core.SearchCalls); // exactly one probe call

        for (var i = 0; i < 15; i++)
        {
            Assert.Empty(await indexer.SearchAlbumsInternalAsync("query", CancellationToken.None));
        }
        Assert.Equal(1, core.SearchCalls); // amplification stopped
    }

    [Fact]
    public async Task SearchTracks_AuthLatchedBad_ShortCircuitsAfterFirstProbe()
    {
        var (indexer, core, handler, _) = CreateIndexer();
        await handler.HandleFailureAsync(new AuthFailure { Message = "bad" });

        var first = await indexer.SearchTracksInternalAsync("q", CancellationToken.None);
        Assert.Empty(first);
        Assert.Equal(1, core.SearchCalls);

        for (var i = 0; i < 10; i++)
        {
            Assert.Empty(await indexer.SearchTracksInternalAsync("q", CancellationToken.None));
        }
        Assert.Equal(1, core.SearchCalls);
    }

    [Fact]
    public async Task SearchAlbums_TidalApiException401_LatchesAuthBad()
    {
        var (indexer, core, handler, _) = CreateIndexer();
        await handler.HandleSuccessAsync();
        core.ThrowFactory = () => new TidalApiException("unauthorized", statusCode: 401);

        await Assert.ThrowsAsync<TidalApiException>(
            () => indexer.SearchAlbumsInternalAsync("q", CancellationToken.None));

        Assert.Equal(AuthStatus.Failed, handler.Status);
    }

    [Fact]
    public async Task SearchAlbums_TidalAuthenticationException_LatchesAuthBad()
    {
        var (indexer, core, handler, _) = CreateIndexer();
        await handler.HandleSuccessAsync();
        core.ThrowFactory = () => new TidalAuthenticationException("token lost");

        await Assert.ThrowsAsync<TidalAuthenticationException>(
            () => indexer.SearchAlbumsInternalAsync("q", CancellationToken.None));

        Assert.Equal(AuthStatus.Failed, handler.Status);
    }

    [Fact]
    public async Task SearchAlbums_500Error_DoesNotLatchAuthBad()
    {
        var (indexer, core, handler, _) = CreateIndexer();
        await handler.HandleSuccessAsync();
        core.ThrowFactory = () => new TidalApiException("server boom", statusCode: 500);

        await Assert.ThrowsAsync<TidalApiException>(
            () => indexer.SearchAlbumsInternalAsync("q", CancellationToken.None));

        Assert.Equal(AuthStatus.Authenticated, handler.Status);
    }

    [Fact]
    public async Task GetAlbumDetails_AuthLatchedBad_ShortCircuitsToEmptyDefault()
    {
        var (indexer, core, handler, _) = CreateIndexer();
        await handler.HandleFailureAsync(new AuthFailure { Message = "bad" });

        // First call consumes the probe slot and hits the core (which returns a stub).
        _ = await indexer.GetAlbumDetailsInternalAsync("alb-1", CancellationToken.None);
        Assert.Equal(1, core.AlbumCalls);

        // Subsequent calls must short-circuit — return the "auth bad" default
        // (Id echoed from the input, empty Title/Artist) without invoking the core.
        var shortCircuited = await indexer.GetAlbumDetailsInternalAsync("alb-2", CancellationToken.None);
        Assert.NotNull(shortCircuited);
        Assert.Equal("alb-2", shortCircuited.Id);
        Assert.Empty(shortCircuited.Title);
        Assert.Equal(1, core.AlbumCalls); // amplification stopped

        for (var i = 0; i < 5; i++)
        {
            await indexer.GetAlbumDetailsInternalAsync("alb-3", CancellationToken.None);
        }
        Assert.Equal(1, core.AlbumCalls);
    }

    [Fact]
    public async Task GateNull_BehavesAsBefore_BackwardsCompat()
    {
        // No gate registered → behavior matches the pre-fix indexer exactly.
        var core = new CountingCore();
        var settings = new TidalIndexerSettings { TidalMarket = "US", RedirectUrl = "https://tidal.com/android/login/auth?code=x&state=y", ConfigPath = Path.GetTempPath() };
        var indexer = new TidalIndexer(
            new TidalSearchService(core, new Tidalarr.Domain.Quality.TidalQualityDetector()),
            core,
            settings,
            NullLogger.Instance);

        for (var i = 0; i < 3; i++)
        {
            await indexer.SearchAlbumsInternalAsync("q", CancellationToken.None);
        }
        Assert.Equal(3, core.SearchCalls); // no gate → no short-circuit
    }
}
