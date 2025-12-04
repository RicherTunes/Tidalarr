using System.Net;
using System.Text;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Api;

namespace Tidalarr.Tests;

public class TidalApiClientInvalidJsonMoreTests
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

    private class BadJsonHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent("not-json", Encoding.UTF8, "application/json") });
        }
    }

    [Fact]
    public async Task GetAlbumAsync_InvalidJson_ThrowsJsonException()
    {
        TidalApiClient client = new TidalApiClient(new HttpClient(new BadJsonHandler()), new Auth());
        _ = await Assert.ThrowsAsync<System.Text.Json.JsonException>(() => client.GetAlbumAsync("al1"));
    }

    [Fact]
    public async Task GetAlbumTracksAsync_InvalidJson_ThrowsJsonException()
    {
        TidalApiClient client = new TidalApiClient(new HttpClient(new BadJsonHandler()), new Auth());
        _ = await Assert.ThrowsAsync<System.Text.Json.JsonException>(() => client.GetAlbumTracksAsync("al1"));
    }

    [Fact]
    public async Task SearchAsync_InvalidJson_ThrowsJsonException()
    {
        TidalApiClient client = new TidalApiClient(new HttpClient(new BadJsonHandler()), new Auth());
        _ = await Assert.ThrowsAsync<System.Text.Json.JsonException>(() => client.SearchAsync("abc"));
    }
}




