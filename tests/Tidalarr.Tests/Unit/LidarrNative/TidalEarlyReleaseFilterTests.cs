using Tidalarr.Core.Models;
using Tidalarr.Integration.LidarrNative;

namespace Tidalarr.Tests.Unit.LidarrNative;

/// <summary>
/// T-3 (dead-settings audit): "Early Release Limit" was accepted, validated, and copied between
/// settings objects but never consulted anywhere in the search pipeline — Tidal albums with a
/// release date arbitrarily far in the future were always surfaced. These tests pin
/// <see cref="TidalEarlyReleaseFilter"/>, the helper that gives the setting a real effect.
/// </summary>
public class TidalEarlyReleaseFilterTests
{
    private static readonly DateTime UtcNow = new(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);

    private static TidalAlbumInfo AlbumReleasing(DateTime releaseDate) => new(
        Id: Guid.NewGuid().ToString(),
        Title: "Test Album",
        Artists: ["Test Artist"],
        Tracks: [],
        AvailableQualities: [TidalQuality.Lossless],
        ReleaseDate: releaseDate,
        CoverArtId: string.Empty,
        IsAvailable: true);

    [Fact]
    public void Apply_WithNullLimit_ReturnsAllAlbums_Unfiltered()
    {
        var albums = new[]
        {
            AlbumReleasing(UtcNow.AddYears(5)),
            AlbumReleasing(UtcNow.AddDays(-1)),
        };

        IReadOnlyList<TidalAlbumInfo> result = TidalEarlyReleaseFilter.Apply(albums, earlyReleaseLimitDays: null, UtcNow);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Apply_WithZeroLimit_ReturnsAllAlbums_Unfiltered()
    {
        // README.md documents "0 = include all" for this field — honor that contract now that the
        // setting has a real implementation for the first time (it was previously dead).
        var farFuture = AlbumReleasing(UtcNow.AddYears(1));
        var albums = new[] { farFuture };

        IReadOnlyList<TidalAlbumInfo> result = TidalEarlyReleaseFilter.Apply(albums, earlyReleaseLimitDays: 0, UtcNow);

        Assert.Single(result);
    }

    [Fact]
    public void Apply_ExcludesAlbums_ReleasingBeyondTheWindow()
    {
        var farFuture = AlbumReleasing(UtcNow.AddDays(30));
        var albums = new[] { farFuture };

        IReadOnlyList<TidalAlbumInfo> result = TidalEarlyReleaseFilter.Apply(albums, earlyReleaseLimitDays: 14, UtcNow);

        Assert.Empty(result);
    }

    [Fact]
    public void Apply_IncludesAlbums_ReleasingWithinTheWindow()
    {
        var soon = AlbumReleasing(UtcNow.AddDays(10));
        var albums = new[] { soon };

        IReadOnlyList<TidalAlbumInfo> result = TidalEarlyReleaseFilter.Apply(albums, earlyReleaseLimitDays: 14, UtcNow);

        Assert.Single(result);
    }

    [Fact]
    public void Apply_IncludesAlbums_ExactlyAtTheWindowBoundary()
    {
        var atBoundary = AlbumReleasing(UtcNow.Date.AddDays(14));
        var albums = new[] { atBoundary };

        IReadOnlyList<TidalAlbumInfo> result = TidalEarlyReleaseFilter.Apply(albums, earlyReleaseLimitDays: 14, UtcNow);

        Assert.Single(result);
    }

    [Fact]
    public void Apply_AlwaysIncludes_AlreadyReleasedAlbums()
    {
        var alreadyOut = AlbumReleasing(UtcNow.AddYears(-3));
        var albums = new[] { alreadyOut };

        IReadOnlyList<TidalAlbumInfo> result = TidalEarlyReleaseFilter.Apply(albums, earlyReleaseLimitDays: 0, UtcNow);

        Assert.Single(result);
    }

    [Fact]
    public void Apply_IncludesAlbums_WithUnknownReleaseDate()
    {
        // Unknown/missing release date is represented as DateTime.MinValue upstream — must never
        // be misread as "far in the future" and dropped.
        var unknownDate = AlbumReleasing(default);
        var albums = new[] { unknownDate };

        IReadOnlyList<TidalAlbumInfo> result = TidalEarlyReleaseFilter.Apply(albums, earlyReleaseLimitDays: 0, UtcNow);

        Assert.Single(result);
    }

    [Fact]
    public void Apply_WithEmptyInput_ReturnsEmpty()
    {
        IReadOnlyList<TidalAlbumInfo> result = TidalEarlyReleaseFilter.Apply([], earlyReleaseLimitDays: 14, UtcNow);

        Assert.Empty(result);
    }

    [Fact]
    public void Apply_WithNullInput_ReturnsEmpty()
    {
        IReadOnlyList<TidalAlbumInfo> result = TidalEarlyReleaseFilter.Apply(null!, earlyReleaseLimitDays: 14, UtcNow);

        Assert.NotNull(result);
        Assert.Empty(result);
    }
}
