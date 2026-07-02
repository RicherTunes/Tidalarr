using Tidalarr.Application.Services;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Quality;

namespace Tidalarr.Tests;

/// <summary>
/// Guards the search-term normalization in <see cref="TidalSearchService"/>.
///
/// Regression: the service used <c>Sanitize.DisplayText</c> (an HTML encoder) to "normalize"
/// the query, which turned every non-ASCII / quote / ampersand character into an HTML entity
/// (e.g. "Record n°V" -> "Record n&#176;V", "Beyoncé" -> "Beyonc&#233;") before the term ever
/// reached the Tidal API — guaranteeing 0 results for any accented or punctuated search. The
/// normalizer must preserve those characters (the request builder URL-encodes later).
/// </summary>
public sealed class TidalSearchServiceSpecialCharTests
{
    private sealed class RecordingCore : ITidalCore
    {
        public string? LastQuery { get; private set; }

        public Task<TidalSearchResults> SearchAsync(string query, int limit = 100, CancellationToken cancellationToken = default)
        {
            LastQuery = query;
            return Task.FromResult(new TidalSearchResults([], [], [], 0, false));
        }

        public Task<TidalTrackInfo> GetTrackAsync(string trackId, CancellationToken cancellationToken = default)
            => Task.FromResult(new TidalTrackInfo("", "", [], "", "", 0, 0, TidalQuality.High, true, DateTime.MinValue));
        public Task<TidalAlbumInfo> GetAlbumAsync(string albumId, CancellationToken cancellationToken = default)
            => Task.FromResult(new TidalAlbumInfo("", "", [], [], [], DateTime.MinValue, "", true));
        public Task<List<TidalTrackInfo>> GetAlbumTracksAsync(string albumId, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<TidalTrackInfo>());
        public Task<TidalAlbumInfo> GetAlbumWithTracksAsync(string albumId, CancellationToken cancellationToken = default)
            => GetAlbumAsync(albumId, cancellationToken);
        public Task<TidalStreamInfo> GetStreamInfoAsync(string trackId, TidalQuality quality, CancellationToken cancellationToken = default)
            => Task.FromResult(new TidalStreamInfo(trackId, [], ".flac", "audio/flac", false, null));
        public Task<bool> IsAuthenticatedAsync() => Task.FromResult(true);
    }

    private static async Task<string?> CaptureQueryAsync(string input)
    {
        var core = new RecordingCore();
        var svc = new TidalSearchService(core, new TidalQualityDetector());
        _ = await svc.SearchWithQualityDetectionAsync(input, TidalQuality.Lossless);
        return core.LastQuery;
    }

    [Fact]
    public async Task DegreeSign_IsNotHtmlEncoded()
    {
        var sent = await CaptureQueryAsync("Bleu Jeans Bleu Record n°V");
        Assert.Contains("n°V", sent);
        Assert.DoesNotContain("&#", sent);
    }

    [Theory]
    [InlineData("Beyoncé")]
    [InlineData("Motörhead")]
    [InlineData("Sigur Rós")]
    public async Task AccentedCharacters_ArePreserved(string input)
    {
        var sent = await CaptureQueryAsync(input);
        Assert.Equal(input, sent);
        Assert.DoesNotContain("&#", sent);
    }

    [Fact]
    public async Task Apostrophe_IsPreserved_NotHtmlEncoded()
    {
        var sent = await CaptureQueryAsync("Guns N' Roses");
        Assert.Contains("'", sent);
        Assert.DoesNotContain("&#39;", sent);
    }

    [Fact]
    public async Task Ampersand_IsPreserved_NotHtmlEncoded()
    {
        var sent = await CaptureQueryAsync("Simon & Garfunkel");
        Assert.Contains("&", sent);
        Assert.DoesNotContain("&amp;", sent);
    }

    [Fact]
    public async Task PlainAscii_PassesThroughUnchanged()
    {
        var sent = await CaptureQueryAsync("Daft Punk Discovery");
        Assert.Equal("Daft Punk Discovery", sent);
    }

    [Fact]
    public async Task SurroundingAndInternalWhitespace_IsCollapsed()
    {
        var sent = await CaptureQueryAsync("  Daft   Punk  ");
        Assert.Equal("Daft Punk", sent);
    }
}
