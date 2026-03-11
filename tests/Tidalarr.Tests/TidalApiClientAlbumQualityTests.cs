using System.Text.Json;
using System.Net;
using System.Text;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Api;

namespace Tidalarr.Tests;

public class TidalApiClientAlbumQualityTests
{
    [Fact]
    public async Task GetAlbumAsync_WithHiResAudioQuality_IncludesHiRes()
    {
        TidalAlbumDto dto = new("al1", "A", new("X", "a1"), DateTime.UtcNow.ToString("yyyy-MM-dd"), 1, 1, true, "c", audioQuality: "HI_RES_LOSSLESS");
        HttpClient http = new(new BodyHandler(JsonSerializer.Serialize(dto)));
        TidalApiClient client = new(http, new AuthStub());
        TidalAlbumInfo album = await client.GetAlbumAsync("al1");
        Assert.Contains(TidalQuality.HiRes, album.AvailableQualities);
        Assert.Contains(TidalQuality.Lossless, album.AvailableQualities);
        Assert.Contains(TidalQuality.High, album.AvailableQualities);
    }

    [Fact]
    public async Task GetAlbumAsync_WithoutHiRes_DoesNotIncludeHiRes()
    {
        TidalAlbumDto dto = new("al1", "A", new("X", "a1"), DateTime.UtcNow.ToString("yyyy-MM-dd"), 1, 1, true, "c", audioQuality: "LOSSLESS");
        HttpClient http = new(new BodyHandler(JsonSerializer.Serialize(dto)));
        TidalApiClient client = new(http, new AuthStub());
        TidalAlbumInfo album = await client.GetAlbumAsync("al1");
        Assert.DoesNotContain(TidalQuality.HiRes, album.AvailableQualities);
        Assert.Contains(TidalQuality.Lossless, album.AvailableQualities);
    }

    private class AuthStub : ITidalAuth
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
            return new("at", "rt", "Bearer", DateTime.UtcNow.AddHours(1), "sess", "US", "uid");
        }
    }

    private class BodyHandler(string body, HttpStatusCode code = HttpStatusCode.OK) : HttpMessageHandler
    {
        private readonly string _body = body;
        private readonly HttpStatusCode _code = code;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpResponseMessage msg = new(this._code)
            {
                Content = new StringContent(this._body, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(msg);
        }
    }
}



