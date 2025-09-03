using System.Text.Json;
using System.Net;
using System.Text;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Api;
using Xunit;

namespace Tidalarr.Tests;

public class TidalApiClientAlbumQualityTests
{
    [Fact]
    public async Task GetAlbumAsync_WithHiResAudioQuality_IncludesHiRes()
    {
        var dto = new TidalAlbumDto("al1","A", new("X","a1"), DateTime.UtcNow.ToString("yyyy-MM-dd"), 1,1,true,"c", audioQuality: "HI_RES_LOSSLESS");
        var http = new HttpClient(new BodyHandler(JsonSerializer.Serialize(dto)));
        var client = new TidalApiClient(http, new AuthStub());
        var album = await client.GetAlbumAsync("al1");
        Assert.Contains(TidalQuality.HiRes, album.AvailableQualities);
        Assert.Contains(TidalQuality.Lossless, album.AvailableQualities);
        Assert.Contains(TidalQuality.High, album.AvailableQualities);
    }

    [Fact]
    public async Task GetAlbumAsync_WithoutHiRes_DoesNotIncludeHiRes()
    {
        var dto = new TidalAlbumDto("al1","A", new("X","a1"), DateTime.UtcNow.ToString("yyyy-MM-dd"), 1,1,true,"c", audioQuality: "LOSSLESS");
        var http = new HttpClient(new BodyHandler(JsonSerializer.Serialize(dto)));
        var client = new TidalApiClient(http, new AuthStub());
        var album = await client.GetAlbumAsync("al1");
        Assert.DoesNotContain(TidalQuality.HiRes, album.AvailableQualities);
        Assert.Contains(TidalQuality.Lossless, album.AvailableQualities);
    }

    private class AuthStub : ITidalAuth
    {
        public bool IsAuthenticated => true;
        public Task<TidalAuthUrl> GenerateAuthUrlAsync() => Task.FromResult(new TidalAuthUrl("","",""));
        public Task<TidalTokens> ExchangeCodeAsync(string authCode, string codeVerifier) => Task.FromResult(Default());
        public Task<TidalTokens> RefreshTokensAsync(string refreshToken) => Task.FromResult(Default());
        public Task<TidalTokens> GetValidTokensAsync() => Task.FromResult(Default());
        private static TidalTokens Default() => new("at","rt","Bearer", DateTime.UtcNow.AddHours(1), "sess","US","uid");
    }

    private class BodyHandler : HttpMessageHandler
    {
        private readonly string _body;
        private readonly HttpStatusCode _code;
        public BodyHandler(string body, HttpStatusCode code = HttpStatusCode.OK) { _body = body; _code = code; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var msg = new HttpResponseMessage(_code)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(msg);
        }
    }
}

