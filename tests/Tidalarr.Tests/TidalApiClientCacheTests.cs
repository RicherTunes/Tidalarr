using Lidarr.Plugin.Common.Interfaces;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Api;

namespace Tidalarr.Tests;

public class TidalApiClientCacheTests
{
    [Fact]
    public async Task GetTrackAsync_UsesCache_WhenPresent()
    {
        HttpClient http = new(new ThrowingHandler());
        StubAuth auth = new();
        PrepopulatedCache cache = new PrepopulatedCache()
            .With("tracks/123", new Dictionary<string, string> { { "sessionId", "sess" }, { "countryCode", "US" } }, new TidalTrackDto(
                id: "123", title: "Title", artist: new("Artist", "a1"), album: new("al1", "Album", new("Artist", "a1"), DateTime.UtcNow.ToString("yyyy-MM-dd"), 10, 3000, true, "cover"), trackNumber: 1, duration: 200, streamReady: true, audioQuality: "LOSSLESS"));

        TidalApiClient client = new(http, auth, cache);
        TidalTrackInfo track = await client.GetTrackAsync("123");
        Assert.Equal("123", track.Id);
        Assert.Equal("Title", track.Title);
    }

    [Fact]
    public async Task GetAlbumAsync_UsesCache_WhenPresent()
    {
        HttpClient http = new(new ThrowingHandler());
        StubAuth auth = new();
        TidalAlbumDto dto = new("al1", "Album", new("Artist", "a1"), DateTime.UtcNow.ToString("yyyy-MM-dd"), 10, 3000, true, "cover");
        PrepopulatedCache cache = new PrepopulatedCache()
            .With("albums/al1", new Dictionary<string, string> { { "sessionId", "sess" }, { "countryCode", "US" } }, dto);

        TidalApiClient client = new(http, auth, cache);
        TidalAlbumInfo album = await client.GetAlbumAsync("al1");
        Assert.Equal("al1", album.Id);
        Assert.Equal("Album", album.Title);
    }

    [Fact]
    public async Task GetAlbumTracksAsync_UsesCache_WhenPresent()
    {
        HttpClient http = new(new ThrowingHandler());
        StubAuth auth = new();
        TidalTrackDto track = new("t1", "Song", new("Artist", "a1"), new("al1", "Album", new("Artist", "a1"), DateTime.UtcNow.ToString("yyyy-MM-dd"), 10, 3000, true, "cover"), 1, 180, true, "LOSSLESS");
        TidalAlbumTracksDto dto = new(new List<TidalTrackDto> { track }, 1);
        PrepopulatedCache cache = new PrepopulatedCache()
            .With("albums/al1/tracks", new Dictionary<string, string> { { "sessionId", "sess" }, { "countryCode", "US" }, { "limit", "1000" } }, dto);

        TidalApiClient client = new(http, auth, cache);
        List<TidalTrackInfo> tracks = await client.GetAlbumTracksAsync("al1");
        _ = Assert.Single(tracks);
        Assert.Equal("t1", tracks[0].Id);
    }

    [Fact]
    public async Task SearchAsync_UsesCache_WhenPresent()
    {
        HttpClient http = new(new ThrowingHandler());
        StubAuth auth = new();
        TidalSearchResponseDto dto = new(new(new()), new(new()));
        PrepopulatedCache cache = new PrepopulatedCache()
            .With("search", new Dictionary<string, string> { { "query", "abc" }, { "types", "albums,tracks" }, { "limit", "100" }, { "sessionId", "sess" }, { "countryCode", "US" } }, dto);

        TidalApiClient client = new(http, auth, cache);
        TidalSearchResults results = await client.SearchAsync("abc");
        Assert.Equal(0, results.TotalCount);
    }
}

internal class ThrowingHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        throw new InvalidOperationException("HTTP should not be called when cache hit present");
    }
}

internal class StubAuth : ITidalAuth
{
    public bool IsAuthenticated => true;
    public Task<TidalAuthUrl> GenerateAuthUrlAsync()
    {
        return Task.FromResult(new TidalAuthUrl("", "", "", string.Empty));
    }

    public Task<TidalTokens> ExchangeCodeAsync(string authCode, string codeVerifier)
    {
        return Task.FromResult(Default());
    }

    public Task<TidalTokens> RefreshTokensAsync(string refreshToken)
    {
        return Task.FromResult(Default());
    }

    public Task<TidalTokens> GetValidTokensAsync()
    {
        return Task.FromResult(Default());
    }

    public TidalCallbackResult ParseCallbackUrl(string callbackUrl)
    {
        return TidalCallbackResult.Failure("Not implemented in test stub");
    }

    private static TidalTokens Default()
    {
        return new("at", "rt", "Bearer", DateTime.UtcNow.AddHours(1), "sess", "US", "u1");
    }
}

internal class PrepopulatedCache : IStreamingResponseCache
{
    private readonly Dictionary<(string endpoint, string key), object> _data = [];

    public PrepopulatedCache With<T>(string endpoint, Dictionary<string, string> parameters, T value) where T : class
    {
        string key = BuildKey(endpoint, parameters);
        this._data[(endpoint, key)] = value!;
        return this;
    }

    private static string BuildKey(string endpoint, Dictionary<string, string> parameters)
    {
        return endpoint + "?" + string.Join("&", parameters.OrderBy(k => k.Key).Select(k => $"{k.Key}={k.Value}"));
    }

    public T? Get<T>(string endpoint, Dictionary<string, string> parameters) where T : class
    {
        string key = BuildKey(endpoint, parameters);
        return this._data.TryGetValue((endpoint, key), out object? obj) ? (T)obj : null;
    }

    public void Set<T>(string endpoint, Dictionary<string, string> parameters, T value) where T : class
    {
        string key = BuildKey(endpoint, parameters);
        this._data[(endpoint, key)] = value!;
    }

    public void Set<T>(string endpoint, Dictionary<string, string> parameters, T value, TimeSpan duration) where T : class
    {
        string key = BuildKey(endpoint, parameters);
        this._data[(endpoint, key)] = value!;
    }

    public bool ShouldCache(string endpoint)
    {
        return true;
    }

    public TimeSpan GetCacheDuration(string endpoint)
    {
        return TimeSpan.FromMinutes(5);
    }

    public string GenerateCacheKey(string endpoint, Dictionary<string, string> parameters)
    {
        return BuildKey(endpoint, parameters);
    }

    public void Clear() { }
    public void ClearEndpoint(string endpoint) { }
}


