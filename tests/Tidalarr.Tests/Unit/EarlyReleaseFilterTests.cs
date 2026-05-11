using Tidalarr.Core.Models;
using Tidalarr.Domain.Filters;

namespace Tidalarr.Tests.Unit;

/// <summary>
/// EarlyReleaseFilter clips pre-release albums whose release date is further in the future
/// than the user-configured EarlyReleaseLimit (in days). Before 2026-05-10 the
/// EarlyReleaseLimit setting was disclosed in the UI as "informational" because no code path
/// actually applied it; this filter is the wire-up.
/// </summary>
public sealed class EarlyReleaseFilterTests
{
    private static readonly DateTimeOffset Now = new(2026, 05, 10, 12, 0, 0, TimeSpan.Zero);

    private static TidalAlbumInfo Album(string id, DateTime releaseDate) => new(
        Id: id,
        Title: $"Album {id}",
        Artists: new[] { "Test Artist" },
        Tracks: Array.Empty<TidalTrackInfo>(),
        AvailableQualities: Array.Empty<TidalQuality>(),
        ReleaseDate: releaseDate,
        CoverArtId: string.Empty,
        IsAvailable: true);

    [Fact]
    public void Filter_NullLimit_ReturnsAllAlbums()
    {
        var albums = new[]
        {
            Album("a", Now.AddDays(0).DateTime),
            Album("b", Now.AddDays(365).DateTime),
            Album("c", Now.AddDays(-30).DateTime)
        };

        var result = EarlyReleaseFilter.Filter(albums, earlyReleaseLimitDays: null, utcNow: Now);

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void Filter_AlreadyReleasedAlbums_AlwaysIncluded()
    {
        var albums = new[]
        {
            Album("past-1", Now.AddDays(-1).DateTime),
            Album("past-1000", Now.AddDays(-1000).DateTime),
            Album("today", Now.DateTime)
        };

        var result = EarlyReleaseFilter.Filter(albums, earlyReleaseLimitDays: 14, utcNow: Now);

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void Filter_FutureWithinLimit_Included()
    {
        var albums = new[]
        {
            Album("in-7-days", Now.AddDays(7).DateTime),
            Album("in-13-days", Now.AddDays(13).DateTime),
            Album("in-14-days", Now.AddDays(14).DateTime)
        };

        var result = EarlyReleaseFilter.Filter(albums, earlyReleaseLimitDays: 14, utcNow: Now);

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void Filter_FutureBeyondLimit_Excluded()
    {
        var albums = new[]
        {
            Album("in-15-days", Now.AddDays(15).DateTime),
            Album("in-100-days", Now.AddDays(100).DateTime),
            Album("in-14-days", Now.AddDays(14).DateTime)
        };

        var result = EarlyReleaseFilter.Filter(albums, earlyReleaseLimitDays: 14, utcNow: Now);

        // Only the 14-day one survives; 15 and 100 are beyond the window.
        Assert.Single(result);
        Assert.Equal("in-14-days", result[0].Id);
    }

    [Fact]
    public void Filter_ZeroLimit_AllowsOnlyReleasedAlbums()
    {
        var albums = new[]
        {
            Album("yesterday", Now.AddDays(-1).DateTime),
            Album("today", Now.DateTime),
            Album("tomorrow", Now.AddDays(1).DateTime)
        };

        var result = EarlyReleaseFilter.Filter(albums, earlyReleaseLimitDays: 0, utcNow: Now);

        // Zero days = today is the boundary; future releases excluded.
        Assert.Equal(2, result.Count);
        Assert.Contains(result, a => a.Id == "yesterday");
        Assert.Contains(result, a => a.Id == "today");
    }

    [Fact]
    public void Filter_NegativeLimit_TreatedAsZero()
    {
        // Defensive: a negative value shouldn't blow up; treat it as "no future releases at all".
        var albums = new[]
        {
            Album("released", Now.AddDays(-1).DateTime),
            Album("future", Now.AddDays(1).DateTime)
        };

        var result = EarlyReleaseFilter.Filter(albums, earlyReleaseLimitDays: -5, utcNow: Now);

        Assert.Single(result);
        Assert.Equal("released", result[0].Id);
    }

    [Fact]
    public void Filter_EmptyInput_ReturnsEmpty()
    {
        var result = EarlyReleaseFilter.Filter(Array.Empty<TidalAlbumInfo>(), earlyReleaseLimitDays: 14, utcNow: Now);
        Assert.Empty(result);
    }

    [Fact]
    public void Filter_PreservesOrder()
    {
        var albums = new[]
        {
            Album("a", Now.AddDays(-10).DateTime),
            Album("b", Now.AddDays(5).DateTime),
            Album("c", Now.AddDays(10).DateTime),
            Album("d", Now.AddDays(50).DateTime), // dropped
            Album("e", Now.AddDays(2).DateTime)
        };

        var result = EarlyReleaseFilter.Filter(albums, earlyReleaseLimitDays: 14, utcNow: Now);

        Assert.Equal(new[] { "a", "b", "c", "e" }, result.Select(a => a.Id));
    }
}
