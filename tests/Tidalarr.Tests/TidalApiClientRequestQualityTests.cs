using System.Text;
using System.Text.Json;
using System.Net;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Api;
using Xunit;

namespace Tidalarr.Tests;

public class TidalApiClientRequestQualityTests
{
    private class Auth : ITidalAuth
    {
        public bool IsAuthenticated => true;
        public Task<TidalAuthUrl> GenerateAuthUrlAsync() => Task.FromResult(new TidalAuthUrl("", "", "", string.Empty));
        public Task<TidalTokens> ExchangeCodeAsync(string authCode, string codeVerifier) => Task.FromResult(Default());
        public Task<TidalTokens> RefreshTokensAsync(string refreshToken) => Task.FromResult(Default());
        public Task<TidalTokens> GetValidTokensAsync() => Task.FromResult(Default());
        private static TidalTokens Default() => new("at", "rt", "Bearer", DateTime.UtcNow.AddHours(1), "sess", "US", "uid");
    }

    private class CaptureHandler : HttpMessageHandler
    {
        private readonly string _response; private readonly HttpStatusCode _code;
        public HttpRequestMessage? Last { get; private set; }
        public CaptureHandler(string response, HttpStatusCode code = HttpStatusCode.OK) { _response = response; _code = code; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Last = request;
            return Task.FromResult(new HttpResponseMessage(_code) { Content = new StringContent(_response, Encoding.UTF8, "application/json") });
        }
    }

    [Theory]
    [InlineData(TidalQuality.Low, "LOW")]
    [InlineData(TidalQuality.High, "HIGH")]
    [InlineData(TidalQuality.Lossless, "LOSSLESS")]
    [InlineData(TidalQuality.HiRes, "HI_RES_LOSSLESS")]
    public async Task GetStreamInfoAsync_IncludesAudioQualityParameter(TidalQuality q, string expectedParam)
    {
        var dto = new TidalPlaybackInfoDto(Convert.ToBase64String(Encoding.UTF8.GetBytes("<MPD/>")), "application/dash+xml", "NONE", null);
        var handler = new CaptureHandler(JsonSerializer.Serialize(dto));
        var api = new TidalApiClient(new HttpClient(handler), new Auth());
        var _ = await api.GetStreamInfoAsync("t1", q);
        Assert.Contains($"audioquality={expectedParam}", handler.Last!.RequestUri!.Query);
    }
}




