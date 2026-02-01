using System.Net;
using System.Text;
using System.Text.Json;
using Tidalarr.Domain.Api;
using Lidarr.Plugin.Common.Interfaces;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;

namespace Tidalarr.Tests;

public class TidalApiClientErrorTests
{
    [Fact]
    public async Task GetTrackAsync_NonSuccessStatus_ThrowsHttpRequestException()
    {
        HttpClient httpClient = new(new ApiMockHttpMessageHandler("", HttpStatusCode.BadRequest));
        MockAuth auth = new();
        TidalApiClient client = new(httpClient, auth, null);

        _ = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetTrackAsync("123"));
    }

    [Fact]
    public async Task GetAlbumAsync_NonSuccessStatus_ThrowsHttpRequestException()
    {
        HttpClient httpClient = new(new ApiMockHttpMessageHandler("", HttpStatusCode.BadGateway));
        MockAuth auth = new();
        TidalApiClient client = new(httpClient, auth, null);

        _ = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAlbumAsync("al1"));
    }

    [Fact]
    public async Task GetAlbumTracksAsync_NonSuccessStatus_ThrowsHttpRequestException()
    {
        HttpClient httpClient = new(new ApiMockHttpMessageHandler("", HttpStatusCode.ServiceUnavailable));
        MockAuth auth = new();
        TidalApiClient client = new(httpClient, auth, null);

        _ = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAlbumTracksAsync("al1"));
    }

    [Fact]
    public async Task SearchAsync_NonSuccessStatus_ThrowsHttpRequestException()
    {
        HttpClient httpClient = new(new ApiMockHttpMessageHandler("", HttpStatusCode.GatewayTimeout));
        MockAuth auth = new();
        TidalApiClient client = new(httpClient, auth, null);

        _ = await Assert.ThrowsAsync<HttpRequestException>(() => client.SearchAsync("abc"));
    }

    [Fact]
    public async Task GetTrackAsync_InvalidJson_ThrowsJsonException()
    {
        HttpClient httpClient = new(new ApiMockHttpMessageHandler("not-json", HttpStatusCode.OK));
        MockAuth auth = new();
        TidalApiClient client = new(httpClient, auth, null);

        _ = await Assert.ThrowsAsync<JsonException>(() => client.GetTrackAsync("123"));
    }

    [Fact]
    public async Task GetStreamInfoAsync_DoesNotCachePlaybackInfo()
    {
        TidalPlaybackInfoDto playbackDto = new(
            manifest: Convert.ToBase64String(Encoding.UTF8.GetBytes("test")),
            manifestMimeType: "application/dash+xml",
            encryptionType: "NONE",
            securityToken: null);

        HttpClient httpClient = new(new ApiMockHttpMessageHandler(JsonSerializer.Serialize(playbackDto), HttpStatusCode.OK));
        MockAuth auth = new();
        SpyCache cache = new();
        TidalApiClient client = new(httpClient, auth, cache);

        TidalStreamInfo info = await client.GetStreamInfoAsync("t1", TidalQuality.Lossless);
        Assert.NotNull(info);
        Assert.False(cache.SetCalled);
    }
}

public class ApiMockHttpMessageHandler(string response, HttpStatusCode code) : HttpMessageHandler
{
    private readonly string _response = response;
    private readonly HttpStatusCode _code = code;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        HttpResponseMessage msg = new(this._code);
        if (!string.IsNullOrEmpty(this._response))
        {
            msg.Content = new StringContent(this._response, Encoding.UTF8, "application/json");
        }
        return Task.FromResult(msg);
    }
}

public class MockAuth : ITidalAuth
{
    public bool IsAuthenticated => true;
    public Task<TidalAuthUrl> GenerateAuthUrlAsync()
    {
        return Task.FromResult(new TidalAuthUrl("u", "v", "s", string.Empty));
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
        return new("at", "rt", "Bearer", DateTime.UtcNow.AddHours(1), "sess", "US", "uid");
    }
}

public class SpyCache : IStreamingResponseCache
{
    public bool SetCalled { get; private set; }
    public T? Get<T>(string endpoint, Dictionary<string, string> parameters) where T : class
    {
        return null;
    }

    public void Set<T>(string endpoint, Dictionary<string, string> parameters, T value) where T : class
    {
        SetCalled = true;
    }

    public void Set<T>(string endpoint, Dictionary<string, string> parameters, T value, TimeSpan duration) where T : class
    {
        SetCalled = true;
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
        return endpoint;
    }

    public void Clear() { }
    public void ClearEndpoint(string endpoint) { }
}


