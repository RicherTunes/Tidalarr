using System.Text.Json;
using System.Net;
using System.Text;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Api;

namespace Tidalarr.Tests;

public class TidalApiClientMappingTests
{
    [Fact]
    public async Task GetTrackAsync_UnknownAudioQuality_MapsToHigh()
    {
        TidalTrackDto dto = new TidalTrackDto(
            id: "t1",
            title: "T",
            artist: new("A", "a1"),
            album: new("al1", "Alb", new("A", "a1"), DateTime.UtcNow.ToString("yyyy-MM-dd"), 1, 1, true, "c"),
            trackNumber: 1,
            duration: 10,
            streamReady: true,
            audioQuality: "UNKNOWN");
        HttpClient http = new HttpClient(new Handler(JsonSerializer.Serialize(dto)));
        TidalApiClient client = new TidalApiClient(http, new Auth());
        TidalTrackInfo track = await client.GetTrackAsync("t1");
        Assert.Equal(TidalQuality.High, track.Quality);
    }

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

    private class Handler(string body, HttpStatusCode code = HttpStatusCode.OK) : HttpMessageHandler
    {
        private readonly string _body = body;
        private readonly HttpStatusCode _code = code;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpResponseMessage msg = new HttpResponseMessage(this._code)
            {
                Content = new StringContent(this._body, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(msg);
        }
    }
}




