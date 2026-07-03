using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Lidarr.Plugin.Common.Errors;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Api;

namespace Tidalarr.Tests;

/// <summary>
/// Favorites-import-list support (feature B4): TidalApiClient.GetFavoriteAlbumsAsync /
/// GetFavoriteArtistsAsync page the user's Tidal library
/// (<c>users/{userId}/favorites/{albums|artists}</c>) — a limit/offset + declared
/// totalNumberOfItems collection whose items are wrapped in a <c>{ created, item }</c>
/// envelope. These tests pin: correct envelope unwrap + mapping, pagination termination
/// (multi-page, single-page-no-followup, empty), loud failure on a stalled page (no silent
/// truncation), and an actionable failure when the session has no UserId.
///
/// host-free-ci: no Lidarr host dependency — must be explicitly re-included after the
/// ExcludeHostBridge=true Tidal*.cs remove in Tidalarr.Tests.csproj.
/// </summary>
public class TidalApiClientFavoritesTests
{
    [Fact]
    public async Task GetFavoriteAlbumsAsync_MultiPage_FetchesAndMapsAllDeclaredAlbums()
    {
        // Server declares 3 favorites but only returns 2 per page — exercises the follow-up page.
        FavoritesHandler handler = new("albums",
        [
            (0, AlbumsPage(total: 3, albums: [(10, "Album Ten", "Artist A"), (11, "Album Eleven", "Artist B")])),
            (2, AlbumsPage(total: 3, albums: [(12, "Album Twelve", "Artist C")])),
        ]);
        TidalApiClient client = new(new HttpClient(handler), new FavoritesAuth());

        List<TidalAlbumInfo> albums = await client.GetFavoriteAlbumsAsync();

        Assert.Equal(3, albums.Count);
        Assert.Equal(["10", "11", "12"], albums.Select(a => a.Id));
        Assert.Equal("Album Ten", albums[0].Title);
        Assert.Contains("Artist A", albums[0].Artists);
        Assert.Equal(2, handler.RequestCount); // proves the second page was actually fetched
        Assert.All(handler.RequestedPaths, p => Assert.Contains("users/uid/favorites/albums", p));
    }

    [Fact]
    public async Task GetFavoriteAlbumsAsync_Empty_ReturnsEmptyListWithoutSecondRequest()
    {
        FavoritesHandler handler = new("albums", [(0, AlbumsPage(total: 0, albums: []))]);
        TidalApiClient client = new(new HttpClient(handler), new FavoritesAuth());

        List<TidalAlbumInfo> albums = await client.GetFavoriteAlbumsAsync();

        Assert.Empty(albums);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task GetFavoriteAlbumsAsync_SinglePage_DoesNotIssueFollowUpRequest()
    {
        FavoritesHandler handler = new("albums",
            [(0, AlbumsPage(total: 2, albums: [(1, "One", "A"), (2, "Two", "B")]))]);
        TidalApiClient client = new(new HttpClient(handler), new FavoritesAuth());

        List<TidalAlbumInfo> albums = await client.GetFavoriteAlbumsAsync();

        Assert.Equal(2, albums.Count);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task GetFavoriteAlbumsAsync_PaginationStalls_ThrowsInsteadOfSilentTruncation()
    {
        // Declares 5 but the second page comes back empty (server stall). Must fail loudly.
        FavoritesHandler handler = new("albums",
        [
            (0, AlbumsPage(total: 5, albums: [(1, "One", "A"), (2, "Two", "B")])),
            (2, AlbumsPage(total: 5, albums: [])),
        ]);
        TidalApiClient client = new(new HttpClient(handler), new FavoritesAuth());

        _ = await Assert.ThrowsAsync<PagedResponseIntegrityException>(
            () => client.GetFavoriteAlbumsAsync());
    }

    [Fact]
    public async Task GetFavoriteArtistsAsync_MultiPage_FetchesAndMapsAllDeclaredArtists()
    {
        FavoritesHandler handler = new("artists",
        [
            (0, ArtistsPage(total: 3, artists: [(100, "Artist One"), (101, "Artist Two")])),
            (2, ArtistsPage(total: 3, artists: [(102, "Artist Three")])),
        ]);
        TidalApiClient client = new(new HttpClient(handler), new FavoritesAuth());

        List<TidalArtistInfo> artists = await client.GetFavoriteArtistsAsync();

        Assert.Equal(3, artists.Count);
        Assert.Equal(["100", "101", "102"], artists.Select(a => a.Id));
        Assert.Equal("Artist One", artists[0].Name);
        Assert.Equal(2, handler.RequestCount);
        Assert.All(handler.RequestedPaths, p => Assert.Contains("users/uid/favorites/artists", p));
    }

    [Fact]
    public async Task GetFavoriteArtistsAsync_Empty_ReturnsEmptyList()
    {
        FavoritesHandler handler = new("artists", [(0, ArtistsPage(total: 0, artists: []))]);
        TidalApiClient client = new(new HttpClient(handler), new FavoritesAuth());

        List<TidalArtistInfo> artists = await client.GetFavoriteArtistsAsync();

        Assert.Empty(artists);
    }

    [Fact]
    public async Task GetFavoriteAlbumsAsync_NoUserId_ThrowsActionable()
    {
        FavoritesHandler handler = new("albums", [(0, AlbumsPage(total: 0, albums: []))]);
        TidalApiClient client = new(new HttpClient(handler), new FavoritesAuth(userId: ""));

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.GetFavoriteAlbumsAsync());

        Assert.Contains("user", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, handler.RequestCount); // never hit the network without a user id
    }

    [Fact]
    public async Task GetFavoriteArtistsAsync_NoUserId_ThrowsActionable()
    {
        FavoritesHandler handler = new("artists", [(0, ArtistsPage(total: 0, artists: []))]);
        TidalApiClient client = new(new HttpClient(handler), new FavoritesAuth(userId: "   "));

        _ = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.GetFavoriteArtistsAsync());
    }

    [Fact]
    public async Task GetFavoriteAlbumsAsync_LogsRedactedEndpointLabel_NotRawUserId()
    {
        const string sensitiveUserId = "sensitive-user-123";
        FavoritesHandler handler = new("albums", [(0, AlbumsPage(total: 0, albums: []))]);
        CapturingLogger<TidalApiClient> logger = new();
        TidalApiClient client = new(
            new HttpClient(handler),
            new FavoritesAuth(userId: sensitiveUserId),
            manifestParser: null,
            logger: logger);

        _ = await client.GetFavoriteAlbumsAsync();

        Assert.Contains(handler.RequestedPaths, p => p.Contains($"users/{sensitiveUserId}/favorites/albums", StringComparison.Ordinal));
        string allLogText = string.Join('\n', logger.Messages);
        Assert.DoesNotContain(sensitiveUserId, allLogText);
        Assert.Contains("users/{userId}/favorites/albums", allLogText);
    }

    private static string AlbumsPage(int total, (int Id, string Title, string Artist)[] albums)
    {
        var items = albums.Select(a => new
        {
            created = "2020-01-01T00:00:00.000+0000",
            item = new
            {
                id = a.Id,
                title = a.Title,
                artist = new { name = a.Artist, id = a.Id + 5000 },
                artists = new[] { new { name = a.Artist, id = a.Id + 5000 } },
                releaseDate = "2020-01-01",
                numberOfTracks = 10,
                streamReady = true,
                cover = "cover-id",
                audioQuality = "LOSSLESS"
            }
        });
        return JsonSerializer.Serialize(new { limit = 50, offset = 0, totalNumberOfItems = total, items });
    }

    private static string ArtistsPage(int total, (int Id, string Name)[] artists)
    {
        var items = artists.Select(a => new
        {
            created = "2020-01-01T00:00:00.000+0000",
            item = new { id = a.Id, name = a.Name }
        });
        return JsonSerializer.Serialize(new { limit = 50, offset = 0, totalNumberOfItems = total, items });
    }

    private sealed class FavoritesHandler(string collection, List<(int Offset, string Json)> pages) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }
        public List<string> RequestedPaths { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            string path = request.RequestUri?.AbsolutePath ?? string.Empty;
            RequestedPaths.Add(path);
            if (!path.Contains($"favorites/{collection}", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Unexpected favorites path: {path}");
            }

            string query = request.RequestUri?.Query ?? string.Empty;
            int offset = 0;
            foreach (string part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] kv = part.Split('=', 2);
                if (kv.Length == 2 && kv[0] == "offset" && int.TryParse(kv[1], out int parsed))
                {
                    offset = parsed;
                }
            }

            string json = pages.FirstOrDefault(p => p.Offset == offset).Json
                ?? throw new InvalidOperationException($"Unexpected page request at offset={offset}");

            HttpResponseMessage response = new(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }

    private sealed class FavoritesAuth(string userId = "uid") : ITidalAuth
    {
        public bool IsAuthenticated => true;

        public Task<TidalAuthUrl> GenerateAuthUrlAsync() => Task.FromResult(new TidalAuthUrl("", "", "", string.Empty));

        public Task<TidalTokens> ExchangeCodeAsync(string authCode, string codeVerifier) => Task.FromResult(Default());

        public Task<TidalTokens> RefreshTokensAsync(string refreshToken) => Task.FromResult(Default());

        public Task<TidalTokens> GetValidTokensAsync() => Task.FromResult(Default());

        public TidalCallbackResult ParseCallbackUrl(string callbackUrl) => TidalCallbackResult.Failure("Not implemented in test stub");

        private TidalTokens Default() => new("at", "rt", "Bearer", DateTime.UtcNow.AddHours(1), "sess", "US", userId);
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }
}
