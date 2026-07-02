using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tidalarr.Core.Models;
using Tidalarr.Integration.LidarrNative;
using Xunit;

namespace Tidalarr.Tests.Unit.LidarrNative;

/// <summary>
/// Pins how <see cref="TidalLidarrIndexer.FetchReleases"/> drives Common's delegate-only
/// <c>SearchPlanExecutor</c> via the <see cref="TidalAlbumSearch"/> adoption: stop-at-first-tier-with-results,
/// combined → artist-only fallback, accumulate-all-variants-in-the-winning-tier, the uniform all-failed
/// <see cref="InvalidOperationException"/> ("Tidal search" label, byte-identical to the pre-adoption
/// message), and the one intended behavior delta — a mid-flight <see cref="OperationCanceledException"/>
/// now propagates instead of being swallowed into the all-failed error.
///
/// <para>These replace the former <c>TidalTieredAlbumSearchTests</c>: the loop mechanics moved to Common
/// (characterized by Common's <c>SearchPlanExecutorTests</c>); this suite characterizes Tidal's wiring of
/// that executor end-to-end through the same adapter the production indexer calls.</para>
/// </summary>
public sealed class TidalAlbumSearchExecutionTests
{
    private static TidalSearchResults WithAlbums(params string[] ids)
    {
        var albums = ids
            .Select(id => new TidalAlbumInfo(id, "Album", ["Artist"], [], [TidalQuality.Lossless], DateTime.UtcNow, "c", true))
            .ToList();
        return new TidalSearchResults(albums, [], [], albums.Count, false);
    }

    private static TidalSearchResults Empty() => new([], [], [], 0, false);

    [Fact]
    public async Task FirstTierWithResults_ShortCircuits_FallbackTiersNotAttempted()
    {
        var called = new List<string>();
        var tiers = new List<IReadOnlyList<string>>
        {
            new[] { "combined" },
            new[] { "artist-only" },
        };

        var albums = await TidalAlbumSearch.ExecuteAsync(
            tiers,
            (q, ct) =>
            {
                called.Add(q);
                return Task.FromResult(q == "combined" ? WithAlbums("a1") : Empty());
            });

        Assert.Equal(new[] { "combined" }, called);
        Assert.Single(albums);
        Assert.Equal("a1", albums[0].Id);
    }

    [Fact]
    public async Task EmptyFirstTier_FallsBackToArtistOnlyTier()
    {
        var called = new List<string>();
        var tiers = new List<IReadOnlyList<string>>
        {
            new[] { "combined" },
            new[] { "artist-only" },
        };

        var albums = await TidalAlbumSearch.ExecuteAsync(
            tiers,
            (q, ct) =>
            {
                called.Add(q);
                return Task.FromResult(q == "artist-only" ? WithAlbums("band1", "band2") : Empty());
            });

        Assert.Equal(new[] { "combined", "artist-only" }, called);
        Assert.Equal(2, albums.Count);
    }

    [Fact]
    public async Task AllVariantsInATierRun_BeforeFallingThrough()
    {
        var called = new List<string>();
        var tiers = new List<IReadOnlyList<string>>
        {
            new[] { "v1", "v2" }, // both combined variants tried before fallback
            new[] { "artist-only" },
        };

        var albums = await TidalAlbumSearch.ExecuteAsync(
            tiers,
            (q, ct) =>
            {
                called.Add(q);
                return Task.FromResult(q == "v2" ? WithAlbums("hit") : Empty());
            });

        // v1 empty, v2 hit -> tier produced results -> do NOT fall through to artist-only.
        Assert.Equal(new[] { "v1", "v2" }, called);
        Assert.Single(albums);
    }

    [Fact]
    public async Task AllTiersEmpty_ReturnsEmpty_NoErrorSurfaced()
    {
        var tiers = new List<IReadOnlyList<string>>
        {
            new[] { "combined" },
            new[] { "artist-only" },
        };

        var albums = await TidalAlbumSearch.ExecuteAsync(tiers, (q, ct) => Task.FromResult(Empty()));

        // A query returning no albums is a SUCCESS, not a failure: no throw, empty result.
        Assert.Empty(albums);
    }

    [Fact]
    public async Task AllRequestsThrow_SurfacesInvalidOperationException_WithTidalSearchLabel()
    {
        var tiers = new List<IReadOnlyList<string>>
        {
            new[] { "combined" },
            new[] { "artist-only" },
        };
        var observed = new List<string>();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            TidalAlbumSearch.ExecuteAsync(
                tiers,
                (q, ct) => throw new InvalidOperationException("boom-" + q),
                onError: (q, _) => observed.Add(q)));

        // Byte-for-byte the message FetchReleases threw before the executor adoption.
        Assert.Equal(
            "All 2 Tidal search request(s) failed; surfacing the error instead of an empty result.",
            ex.Message);
        Assert.IsType<InvalidOperationException>(ex.InnerException);
        Assert.Equal("boom-artist-only", ex.InnerException!.Message); // last error wrapped
        Assert.Equal(new[] { "combined", "artist-only" }, observed); // onError fired per failed variant
    }

    [Fact]
    public async Task ThrowingFirstTier_StillFallsBackAndRecovers()
    {
        var tiers = new List<IReadOnlyList<string>>
        {
            new[] { "combined" },
            new[] { "artist-only" },
        };
        var observed = new List<string>();

        var albums = await TidalAlbumSearch.ExecuteAsync(
            tiers,
            (q, ct) => q == "combined"
                ? throw new InvalidOperationException("transient")
                : Task.FromResult(WithAlbums("recovered")),
            onError: (q, _) => observed.Add(q));

        // Transient failure on the combined tier does NOT abort: the artist-only fallback rescues it.
        Assert.Single(albums);
        Assert.Equal("recovered", albums[0].Id);
        Assert.Equal(new[] { "combined" }, observed); // only the failed variant hit onError
    }

    [Fact]
    public async Task CancellationMidFlight_PropagatesOperationCanceled_NotSwallowed()
    {
        // The one intended behavior delta vs the former local TidalTieredAlbumSearch (which caught OCE
        // into LastError and surfaced it as a generic all-failed InvalidOperationException). Common's
        // executor rethrows a genuine mid-flight cancellation so Lidarr sees a real cancellation.
        var tiers = new List<IReadOnlyList<string>>
        {
            new[] { "combined" },
            new[] { "artist-only" },
        };
        var onErrorCalls = 0;

        using var cts = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            TidalAlbumSearch.ExecuteAsync(
                tiers,
                (q, ct) =>
                {
                    cts.Cancel();
                    throw new OperationCanceledException(cts.Token);
                },
                onError: (q, _) => onErrorCalls++,
                cancellationToken: cts.Token));

        // Cancellation is never recorded as a failed variant nor routed to onError.
        Assert.Equal(0, onErrorCalls);
    }

    [Fact]
    public async Task NullDelegate_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            TidalAlbumSearch.ExecuteAsync(
                new List<IReadOnlyList<string>> { new[] { "q" } },
                searchAsync: null!));
    }
}
