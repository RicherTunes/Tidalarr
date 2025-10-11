using System.Net;
using System.Text.Json;
using Lidarr.Plugin.Common.Interfaces;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Api;
using Xunit;

namespace Tidalarr.Tests;

public class TidalApiClientCacheTests
{
    [Fact]
    public async Task GetTrackAsync_UsesCache_WhenPresent()
    {
        var http = new HttpClient(new ThrowingHandler());
        var auth = new StubAuth();
        var cache = new PrepopulatedCache()
            .With("tracks/123", new Dictionary<string, string> { { "sessionId", "sess" }, { "countryCode", "US" } }, new TidalTrackDto(
                id: "123", title: "Title", artist: new("Artist", "a1"), album: new("al1", "Album", new("Artist", "a1"), DateTime.UtcNow.ToString("yyyy-MM-dd"), 10, 3000, true, "cover"), trackNumber: 1, duration: 200, streamReady: true, audioQuality: "LOSSLESS"));

        var client = new TidalApiClient(http, auth, cache);
        var track = await client.GetTrackAsync("123");
        Assert.Equal("123", track.Id);
        Assert.Equal("Title", track.Title);
    }

    [Fact]
    public async Task GetAlbumAsync_UsesCache_WhenPresent()
    {
        var http = new HttpClient(new ThrowingHandler());
        var auth = new StubAuth();
        var dto = new TidalAlbumDto("al1", "Album", new("Artist", "a1"), DateTime.UtcNow.ToString("yyyy-MM-dd"), 10, 3000, true, "cover");
        var cache = new PrepopulatedCache()
            .With("albums/al1", new Dictionary<string, string> { { "sessionId", "sess" }, { "countryCode", "US" } }, dto);

        var client = new TidalApiClient(http, auth, cache);
        var album = await client.GetAlbumAsync("al1");
        Assert.Equal("al1", album.Id);
        Assert.Equal("Album", album.Title);
    }

    [Fact]
    public async Task GetAlbumTracksAsync_UsesCache_WhenPresent()
    {
        var http = new HttpClient(new ThrowingHandler());
        var auth = new StubAuth();
        var track = new TidalTrackDto("t1", "Song", new("Artist", "a1"), new("al1", "Album", new("Artist", "a1"), DateTime.UtcNow.ToString("yyyy-MM-dd"), 10, 3000, true, "cover"), 1, 180, true, "LOSSLESS");
        var dto = new TidalAlbumTracksDto(new List<TidalTrackDto> { track }, 1);
        var cache = new PrepopulatedCache()
            .With("albums/al1/tracks", new Dictionary<string, string> { { "sessionId", "sess" }, { "countryCode", "US" }, { "limit", "1000" } }, dto);

        var client = new TidalApiClient(http, auth, cache);
        var tracks = await client.GetAlbumTracksAsync("al1");
        Assert.Single(tracks);
        Assert.Equal("t1", tracks[0].Id);
    }

    [Fact]
    public async Task SearchAsync_UsesCache_WhenPresent()
    {
        var http = new HttpClient(new ThrowingHandler());
        var auth = new StubAuth();
        var dto = new TidalSearchResponseDto(new(new()), new(new()));
        var cache = new PrepopulatedCache()
            .With("search", new Dictionary<string, string> { { "query", "abc" }, { "types", "albums,tracks" }, { "limit", "100" }, { "sessionId", "sess" }, { "countryCode", "US" } }, dto);

        var client = new TidalApiClient(http, auth, cache);
        var results = await client.SearchAsync("abc");
        Assert.Equal(0, results.TotalCount);
    }
}

class ThrowingHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => throw new InvalidOperationException("HTTP should not be called when cache hit present");
}

class StubAuth : ITidalAuth
{
    public bool IsAuthenticated => true;
    public Task<TidalAuthUrl> GenerateAuthUrlAsync() => Task.FromResult(new TidalAuthUrl("", "", "", string.Empty));
    public Task<TidalTokens> ExchangeCodeAsync(string authCode, string codeVerifier) => Task.FromResult(Default());
    public Task<TidalTokens> RefreshTokensAsync(string refreshToken) => Task.FromResult(Default());
    public Task<TidalTokens> GetValidTokensAsync() => Task.FromResult(Default());
    private static TidalTokens Default() => new("at", "rt", "Bearer", DateTime.UtcNow.AddHours(1), "sess", "US", "u1");
}

class PrepopulatedCache : IStreamingResponseCache
{
    private readonly Dictionary<(string endpoint, string key), object> _data = new();

    public PrepopulatedCache With<T>(string endpoint, Dictionary<string, string> parameters, T value) where T : class
    {
        var key = BuildKey(endpoint, parameters);
        _data[(endpoint, key)] = value!;
        return this;
    }

    private static string BuildKey(string endpoint, Dictionary<string, string> parameters)
        => endpoint + "?" + string.Join("&", parameters.OrderBy(k => k.Key).Select(k => $"{k.Key}={k.Value}"));

    public T? Get<T>(string endpoint, Dictionary<string, string> parameters) where T : class
    {
        var key = BuildKey(endpoint, parameters);
        return _data.TryGetValue((endpoint, key), out var obj) ? (T)obj : null;
    }

    public void Set<T>(string endpoint, Dictionary<string, string> parameters, T value) where T : class
    {
        var key = BuildKey(endpoint, parameters);
        _data[(endpoint, key)] = value!;
    }

    public void Set<T>(string endpoint, Dictionary<string, string> parameters, T value, TimeSpan duration) where T : class
    {
        var key = BuildKey(endpoint, parameters);
        _data[(endpoint, key)] = value!;
    }

    public bool ShouldCache(string endpoint) => true;
    public TimeSpan GetCacheDuration(string endpoint) => TimeSpan.FromMinutes(5);
    public string GenerateCacheKey(string endpoint, Dictionary<string, string> parameters) => BuildKey(endpoint, parameters);
    public void Clear() { }
    public void ClearEndpoint(string endpoint) { }
}



