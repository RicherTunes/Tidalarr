using System.Text;
using System.Text.Json;
using System.Net;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Api;

namespace Tidalarr.Tests;

public class TidalApiClientRequestQualityTests
{
    private class Auth : ITidalAuth
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

        private static TidalTokens Default()
        {
            return new("at", "rt", "Bearer", DateTime.UtcNow.AddHours(1), "sess", "US", "uid");
        }
    }

    private class CaptureHandler(string response, HttpStatusCode code = HttpStatusCode.OK) : HttpMessageHandler
    {
        private readonly string _response = response; private readonly HttpStatusCode _code = code;
        public HttpRequestMessage? Last { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Last = request;
            return Task.FromResult(new HttpResponseMessage(this._code) { Content = new StringContent(this._response, Encoding.UTF8, "application/json") });
        }
    }

    [Theory]
    [InlineData(TidalQuality.Low, "LOW")]
    [InlineData(TidalQuality.High, "HIGH")]
    [InlineData(TidalQuality.Lossless, "LOSSLESS")]
    [InlineData(TidalQuality.HiRes, "HI_RES_LOSSLESS")]
    public async Task GetStreamInfoAsync_IncludesAudioQualityParameter(TidalQuality q, string expectedParam)
    {
        TidalPlaybackInfoDto dto = new(Convert.ToBase64String(Encoding.UTF8.GetBytes("<MPD/>")), "application/dash+xml", "NONE", null);
        CaptureHandler handler = new(JsonSerializer.Serialize(dto));
        TidalApiClient api = new(new HttpClient(handler), new Auth());
        TidalStreamInfo _ = await api.GetStreamInfoAsync("t1", q);
        Assert.Contains($"audioquality={expectedParam}", handler.Last!.RequestUri!.Query);
    }
}




