using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tidalarr.Application.Services;
using Tidalarr.Core.Exceptions;
using Tidalarr.Core.Models;
using Xunit;

namespace Tidalarr.Tests.Unit;

/// <summary>
/// The parser-side suppression gate: a suppressed album is withheld from AUTOMATIC/RSS searches (the
/// re-grab-loop driver) but OFFERED on an INTERACTIVE (user-initiated) search so a user who has, e.g.,
/// changed region can recover the album without waiting out the 30-day TTL. Mirrors qobuz's
/// <c>QobuzParserSuppressionTests</c> intent, adapted to Tidal's album-list architecture.
/// </summary>
public sealed class TidalReleaseSuppressionFilterTests
{
    [Fact]
    public void Apply_AutomaticSearch_WithholdsSuppressedAlbum()
    {
        var store = new FakeStore("suppressed-album");
        var albums = new[] { Album("suppressed-album"), Album("allowed-album") };

        var result = TidalReleaseSuppressionFilter.Apply(albums, store, isInteractiveSearch: false);

        Assert.DoesNotContain(result, a => a.Id == "suppressed-album");
        Assert.Contains(result, a => a.Id == "allowed-album");
    }

    [Fact]
    public void Apply_InteractiveSearch_OffersSuppressedAlbum_UserOverride()
    {
        var store = new FakeStore("suppressed-album");
        var albums = new[] { Album("suppressed-album"), Album("allowed-album") };

        var result = TidalReleaseSuppressionFilter.Apply(albums, store, isInteractiveSearch: true);

        Assert.Contains(result, a => a.Id == "suppressed-album");
        Assert.Contains(result, a => a.Id == "allowed-album");
    }

    [Fact]
    public void Apply_NoSuppression_ReturnsAllAlbums()
    {
        var store = new FakeStore(/* nothing suppressed */);
        var albums = new[] { Album("a"), Album("b") };

        var result = TidalReleaseSuppressionFilter.Apply(albums, store, isInteractiveSearch: false);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Apply_NullStore_ReturnsAllAlbums()
    {
        var albums = new[] { Album("a"), Album("b") };

        var result = TidalReleaseSuppressionFilter.Apply(albums, store: null, isInteractiveSearch: false);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Apply_AlbumWithBlankId_IsNeverWithheld()
    {
        // A blank id can never be a suppression key; it must pass through unchanged rather than
        // being dropped by a NormalizeReleaseId collision.
        var store = new FakeStore("suppressed-album");
        var albums = new[] { Album(""), Album("suppressed-album") };

        var result = TidalReleaseSuppressionFilter.Apply(albums, store, isInteractiveSearch: false);

        Assert.Contains(result, a => a.Id == "");
        Assert.DoesNotContain(result, a => a.Id == "suppressed-album");
    }

    private static TidalAlbumInfo Album(string id) => new(
        Id: id,
        Title: "Album " + id,
        Artists: new[] { "Artist" },
        Tracks: Array.Empty<TidalTrackInfo>(),
        AvailableQualities: new[] { TidalQuality.Lossless },
        ReleaseDate: new DateTime(2020, 1, 1),
        CoverArtId: "cover",
        IsAvailable: true);

    private sealed class FakeStore : ITidalReleaseSuppressionStore
    {
        private readonly HashSet<string> _suppressed;

        public FakeStore(params string[] suppressed)
            => _suppressed = new HashSet<string>(suppressed, StringComparer.OrdinalIgnoreCase);

        public bool IsSuppressed(string albumId) => !string.IsNullOrWhiteSpace(albumId) && _suppressed.Contains(albumId);

        public Task SuppressAsync(string albumId, string trackId, TidalStreamUnavailableReason reason, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<bool> ClearAsync(string albumId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
    }
}
