using FluentValidation.Results;
using NzbDrone.Core.Parser.Model;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Integration.LidarrNative;

namespace Tidalarr.Tests.Unit.LidarrNative;

/// <summary>
/// Feature B4 — Tidal Favorites import list. Covers the host-coupled seams the API-level
/// favorites tests can't reach: TidalAlbumInfo/TidalArtistInfo -&gt; ImportListItemInfo mapping,
/// content-selection (albums/artists/both), de-duplication, and Test() auth validation
/// (fails cleanly, never throws, when there's no UserId).
///
/// Host-coupled (references NzbDrone.Core.ImportLists + FluentValidation.Results) — excluded from
/// the ExcludeHostBridge=true host-free CI build via Tidalarr.Tests.csproj.
/// </summary>
public class TidalFavoritesImportListTests
{
    [Fact]
    public async Task FetchFavoritesAsync_Both_MapsAlbumsAndArtists()
    {
        StubCore core = new(
            albums: [Album("10", "Kind of Blue", "Miles Davis"), Album("11", "Blue Train", "John Coltrane")],
            artists: [Artist("100", "Bill Evans")]);

        IList<ImportListItemInfo> items = await TidalFavoritesImportList.FetchFavoritesAsync(core, TidalFavoritesContent.AlbumsAndArtists);

        Assert.Equal(3, items.Count);
        ImportListItemInfo album = Assert.Single(items, i => i.Album == "Kind of Blue");
        Assert.Equal("Miles Davis", album.Artist);
        ImportListItemInfo artistOnly = Assert.Single(items, i => i.Artist == "Bill Evans");
        Assert.True(string.IsNullOrEmpty(artistOnly.Album)); // artist favorite has no album
        Assert.True(core.AlbumsFetched);
        Assert.True(core.ArtistsFetched);
    }

    [Fact]
    public async Task FetchFavoritesAsync_AlbumsOnly_DoesNotFetchArtists()
    {
        StubCore core = new(
            albums: [Album("10", "Kind of Blue", "Miles Davis")],
            artists: [Artist("100", "Bill Evans")]);

        IList<ImportListItemInfo> items = await TidalFavoritesImportList.FetchFavoritesAsync(core, TidalFavoritesContent.AlbumsOnly);

        _ = Assert.Single(items);
        Assert.Equal("Kind of Blue", items[0].Album);
        Assert.True(core.AlbumsFetched);
        Assert.False(core.ArtistsFetched); // optimization: artists endpoint not hit when albums-only
    }

    [Fact]
    public async Task FetchFavoritesAsync_ArtistsOnly_DoesNotFetchAlbums()
    {
        StubCore core = new(
            albums: [Album("10", "Kind of Blue", "Miles Davis")],
            artists: [Artist("100", "Bill Evans")]);

        IList<ImportListItemInfo> items = await TidalFavoritesImportList.FetchFavoritesAsync(core, TidalFavoritesContent.ArtistsOnly);

        _ = Assert.Single(items);
        Assert.Equal("Bill Evans", items[0].Artist);
        Assert.False(core.AlbumsFetched);
        Assert.True(core.ArtistsFetched);
    }

    [Fact]
    public async Task FetchFavoritesAsync_Empty_ReturnsEmpty()
    {
        StubCore core = new(albums: [], artists: []);

        IList<ImportListItemInfo> items = await TidalFavoritesImportList.FetchFavoritesAsync(core, TidalFavoritesContent.AlbumsAndArtists);

        Assert.Empty(items);
    }

    [Fact]
    public async Task FetchFavoritesAsync_DuplicateFavorites_Collapsed()
    {
        // Same album favorited twice (e.g. different editions surfaced with same name) + an artist
        // duplicated across the artist list — must not enqueue twice.
        StubCore core = new(
            albums: [Album("10", "Kind of Blue", "Miles Davis"), Album("99", "kind of blue", "miles davis")],
            artists: [Artist("100", "Bill Evans"), Artist("101", "Bill Evans")]);

        IList<ImportListItemInfo> items = await TidalFavoritesImportList.FetchFavoritesAsync(core, TidalFavoritesContent.AlbumsAndArtists);

        Assert.Equal(2, items.Count); // one album + one artist
    }

    [Fact]
    public async Task FetchFavoritesAsync_AlbumMissingArtistOrTitle_Skipped()
    {
        StubCore core = new(
            albums:
            [
                new TidalAlbumInfo("1", "", ["Someone"], [], [], DateTime.MinValue, "", true),      // no title
                new TidalAlbumInfo("2", "Titled", [], [], [], DateTime.MinValue, "", true),          // no artist
                Album("3", "Good", "Real Artist"),
            ],
            artists: [new TidalArtistInfo("9", "  ", null, null, null)]);                            // blank name

        IList<ImportListItemInfo> items = await TidalFavoritesImportList.FetchFavoritesAsync(core, TidalFavoritesContent.AlbumsAndArtists);

        ImportListItemInfo only = Assert.Single(items);
        Assert.Equal("Good", only.Album);
    }

    [Fact]
    public async Task ValidateAuthAsync_NoUserId_AddsActionableFailure()
    {
        StubAuth auth = new(userId: "");
        List<ValidationFailure> failures = [];

        await TidalFavoritesImportList.ValidateAuthAsync(auth, failures);

        ValidationFailure failure = Assert.Single(failures);
        Assert.Contains("authenticate", failure.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateAuthAsync_ValidUserId_NoFailure()
    {
        StubAuth auth = new(userId: "12345");
        List<ValidationFailure> failures = [];

        await TidalFavoritesImportList.ValidateAuthAsync(auth, failures);

        Assert.Empty(failures);
    }

    [Fact]
    public async Task ValidateAuthAsync_AuthThrows_AddsFailureNeverPropagates()
    {
        ThrowingAuth auth = new();
        List<ValidationFailure> failures = [];

        // Must not throw — a broken session surfaces as a validation failure, not a crash.
        await TidalFavoritesImportList.ValidateAuthAsync(auth, failures);

        _ = Assert.Single(failures);
    }

    private static TidalAlbumInfo Album(string id, string title, string artist) =>
        new(id, title, [artist], [], [], DateTime.MinValue, "", true);

    private static TidalArtistInfo Artist(string id, string name) => new(id, name, null, null, null);

    private sealed class StubCore(List<TidalAlbumInfo> albums, List<TidalArtistInfo> artists) : ITidalCore
    {
        public bool AlbumsFetched { get; private set; }
        public bool ArtistsFetched { get; private set; }

        public Task<List<TidalAlbumInfo>> GetFavoriteAlbumsAsync(CancellationToken cancellationToken = default)
        {
            AlbumsFetched = true;
            return Task.FromResult(albums);
        }

        public Task<List<TidalArtistInfo>> GetFavoriteArtistsAsync(CancellationToken cancellationToken = default)
        {
            ArtistsFetched = true;
            return Task.FromResult(artists);
        }

        public Task<TidalTrackInfo> GetTrackAsync(string trackId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<TidalAlbumInfo> GetAlbumAsync(string albumId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<TidalTrackInfo>> GetAlbumTracksAsync(string albumId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<TidalAlbumInfo> GetAlbumWithTracksAsync(string albumId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<TidalSearchResults> SearchAsync(string query, int limit = 100, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<TidalStreamInfo> GetStreamInfoAsync(string trackId, TidalQuality quality, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> IsAuthenticatedAsync() => Task.FromResult(true);
    }

    private sealed class StubAuth(string userId) : ITidalAuth
    {
        public bool IsAuthenticated => true;
        public Task<TidalAuthUrl> GenerateAuthUrlAsync() => Task.FromResult(new TidalAuthUrl("", "", "", string.Empty));
        public Task<TidalTokens> ExchangeCodeAsync(string authCode, string codeVerifier) => Task.FromResult(Tokens());
        public Task<TidalTokens> RefreshTokensAsync(string refreshToken) => Task.FromResult(Tokens());
        public Task<TidalTokens> GetValidTokensAsync() => Task.FromResult(Tokens());
        public TidalCallbackResult ParseCallbackUrl(string callbackUrl) => TidalCallbackResult.Failure("n/a");
        private TidalTokens Tokens() => new("at", "rt", "Bearer", DateTime.UtcNow.AddHours(1), "sess", "US", userId);
    }

    private sealed class ThrowingAuth : ITidalAuth
    {
        public bool IsAuthenticated => false;
        public Task<TidalAuthUrl> GenerateAuthUrlAsync() => Task.FromResult(new TidalAuthUrl("", "", "", string.Empty));
        public Task<TidalTokens> ExchangeCodeAsync(string authCode, string codeVerifier) => throw new InvalidOperationException("no token");
        public Task<TidalTokens> RefreshTokensAsync(string refreshToken) => throw new InvalidOperationException("no token");
        public Task<TidalTokens> GetValidTokensAsync() => throw new InvalidOperationException("no stored token");
        public TidalCallbackResult ParseCallbackUrl(string callbackUrl) => TidalCallbackResult.Failure("n/a");
    }
}
