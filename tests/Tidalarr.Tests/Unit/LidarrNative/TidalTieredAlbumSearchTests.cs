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
/// The tiered-search executor implements the "combined-empty -> artist-only fallback"
/// behavior: tiers run in order and the first tier that yields any albums short-circuits
/// the remaining (fallback) tiers.
/// </summary>
public sealed class TidalTieredAlbumSearchTests
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

        var outcome = await TidalTieredAlbumSearch.RunAsync(
            tiers,
            (q, ct) =>
            {
                called.Add(q);
                return Task.FromResult(q == "combined" ? WithAlbums("a1") : Empty());
            });

        Assert.Equal(new[] { "combined" }, called);
        Assert.Single(outcome.Albums);
        Assert.Equal("a1", outcome.Albums[0].Id);
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

        var outcome = await TidalTieredAlbumSearch.RunAsync(
            tiers,
            (q, ct) =>
            {
                called.Add(q);
                return Task.FromResult(q == "artist-only" ? WithAlbums("band1", "band2") : Empty());
            });

        Assert.Equal(new[] { "combined", "artist-only" }, called);
        Assert.Equal(2, outcome.Albums.Count);
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

        var outcome = await TidalTieredAlbumSearch.RunAsync(
            tiers,
            (q, ct) =>
            {
                called.Add(q);
                return Task.FromResult(q == "v2" ? WithAlbums("hit") : Empty());
            });

        // v1 empty, v2 hit -> tier produced results -> do NOT fall through to artist-only.
        Assert.Equal(new[] { "v1", "v2" }, called);
        Assert.Single(outcome.Albums);
    }

    [Fact]
    public async Task AllTiersEmpty_ReturnsEmpty_NoErrorSurfaced()
    {
        var tiers = new List<IReadOnlyList<string>>
        {
            new[] { "combined" },
            new[] { "artist-only" },
        };

        var outcome = await TidalTieredAlbumSearch.RunAsync(tiers, (q, ct) => Task.FromResult(Empty()));

        Assert.Empty(outcome.Albums);
        Assert.True(outcome.Attempted > 0);
        Assert.True(outcome.Succeeded > 0);
        Assert.Null(outcome.LastError);
    }

    [Fact]
    public async Task AllRequestsThrow_SurfacesLastError_ZeroSucceeded()
    {
        var tiers = new List<IReadOnlyList<string>>
        {
            new[] { "combined" },
            new[] { "artist-only" },
        };
        var observed = new List<string>();

        var outcome = await TidalTieredAlbumSearch.RunAsync(
            tiers,
            (q, ct) => throw new InvalidOperationException("boom-" + q),
            onError: (q, ex) => observed.Add(q));

        Assert.Empty(outcome.Albums);
        Assert.Equal(2, outcome.Attempted);
        Assert.Equal(0, outcome.Succeeded);
        Assert.NotNull(outcome.LastError);
        Assert.Equal(new[] { "combined", "artist-only" }, observed);
    }

    [Fact]
    public async Task ThrowingFirstTier_StillFallsBackAndRecovers()
    {
        var tiers = new List<IReadOnlyList<string>>
        {
            new[] { "combined" },
            new[] { "artist-only" },
        };

        var outcome = await TidalTieredAlbumSearch.RunAsync(
            tiers,
            (q, ct) => q == "combined"
                ? throw new InvalidOperationException("transient")
                : Task.FromResult(WithAlbums("recovered")),
            onError: (q, ex) => { });

        Assert.Single(outcome.Albums);
        Assert.Equal("recovered", outcome.Albums[0].Id);
        Assert.Equal(1, outcome.Succeeded);
        Assert.NotNull(outcome.LastError); // the combined-tier failure is still recorded
    }
}
