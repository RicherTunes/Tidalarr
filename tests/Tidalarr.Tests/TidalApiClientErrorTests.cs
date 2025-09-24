using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;
using Tidalarr.Domain.Api;
using Lidarr.Plugin.Common.Interfaces;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Lidarr.Plugin.Common.Services.Caching;

namespace Tidalarr.Tests;

public class TidalApiClientErrorTests
{
    [Fact]
    public async Task GetTrackAsync_NonSuccessStatus_ThrowsHttpRequestException()
    {
        var httpClient = new HttpClient(new ApiMockHttpMessageHandler("", HttpStatusCode.BadRequest));
        var auth = new MockAuth();
        var client = new TidalApiClient(httpClient, auth);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetTrackAsync("123"));
    }

    [Fact]
    public async Task GetAlbumAsync_NonSuccessStatus_ThrowsHttpRequestException()
    {
        var httpClient = new HttpClient(new ApiMockHttpMessageHandler("", HttpStatusCode.BadGateway));
        var auth = new MockAuth();
        var client = new TidalApiClient(httpClient, auth);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAlbumAsync("al1"));
    }

    [Fact]
    public async Task GetAlbumTracksAsync_NonSuccessStatus_ThrowsHttpRequestException()
    {
        var httpClient = new HttpClient(new ApiMockHttpMessageHandler("", HttpStatusCode.ServiceUnavailable));
        var auth = new MockAuth();
        var client = new TidalApiClient(httpClient, auth);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAlbumTracksAsync("al1"));
    }

    [Fact]
    public async Task SearchAsync_NonSuccessStatus_ThrowsHttpRequestException()
    {
        var httpClient = new HttpClient(new ApiMockHttpMessageHandler("", HttpStatusCode.GatewayTimeout));
        var auth = new MockAuth();
        var client = new TidalApiClient(httpClient, auth);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.SearchAsync("abc"));
    }

    [Fact]
    public async Task GetTrackAsync_InvalidJson_ThrowsJsonException()
    {
        var httpClient = new HttpClient(new ApiMockHttpMessageHandler("not-json", HttpStatusCode.OK));
        var auth = new MockAuth();
        var client = new TidalApiClient(httpClient, auth);

        await Assert.ThrowsAsync<System.Text.Json.JsonException>(() => client.GetTrackAsync("123"));
    }

    [Fact]
    public async Task GetStreamInfoAsync_DoesNotCachePlaybackInfo()
    {
        var playbackDto = new TidalPlaybackInfoDto(
            manifest: Convert.ToBase64String(Encoding.UTF8.GetBytes("test")),
            manifestMimeType: "application/dash+xml",
            encryptionType: "NONE",
            securityToken: null);

        var httpClient = new HttpClient(new ApiMockHttpMessageHandler(JsonSerializer.Serialize(playbackDto), HttpStatusCode.OK));
        var auth = new MockAuth();
        var cache = new SpyCache();
        var client = new TidalApiClient(httpClient, auth, cache);

        var info = await client.GetStreamInfoAsync("t1", TidalQuality.Lossless);
        Assert.NotNull(info);
        Assert.False(cache.SetCalled);
    }
}

public class ApiMockHttpMessageHandler : HttpMessageHandler
{
    private readonly string _response;
    private readonly HttpStatusCode _code;

    public ApiMockHttpMessageHandler(string response, HttpStatusCode code)
    {
        _response = response;
        _code = code;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var msg = new HttpResponseMessage(_code);
        if (!string.IsNullOrEmpty(_response))
        {
            msg.Content = new StringContent(_response, Encoding.UTF8, "application/json");
        }
        return Task.FromResult(msg);
    }
}

public class MockAuth : ITidalAuth
{
    public bool IsAuthenticated => true;
    public Task<TidalAuthUrl> GenerateAuthUrlAsync() => Task.FromResult(new TidalAuthUrl("u","v","s", string.Empty));
    public Task<TidalTokens> ExchangeCodeAsync(string authCode, string codeVerifier) => Task.FromResult(Default());
    public Task<TidalTokens> RefreshTokensAsync(string refreshToken) => Task.FromResult(Default());
    public Task<TidalTokens> GetValidTokensAsync() => Task.FromResult(Default());

    private static TidalTokens Default() => new("at","rt","Bearer", DateTime.UtcNow.AddHours(1), "sess","US","uid");
}

public class SpyCache : IStreamingResponseCache
{
    public bool SetCalled { get; private set; }
    public T? Get<T>(string endpoint, Dictionary<string, string> parameters) where T : class => null;
    public void Set<T>(string endpoint, Dictionary<string, string> parameters, T value) where T : class => SetCalled = true;
    public void Set<T>(string endpoint, Dictionary<string, string> parameters, T value, TimeSpan duration) where T : class => SetCalled = true;
    public bool ShouldCache(string endpoint) => true;
    public TimeSpan GetCacheDuration(string endpoint) => TimeSpan.FromMinutes(5);
    public string GenerateCacheKey(string endpoint, Dictionary<string, string> parameters) => endpoint;
    public void Clear() { }
    public void ClearEndpoint(string endpoint) { }
}
