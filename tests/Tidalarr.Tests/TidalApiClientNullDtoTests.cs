using System.Net;
using System.Text;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Api;

namespace Tidalarr.Tests;

public class TidalApiClientNullDtoTests
{
    [Fact]
    public async Task GetAlbumAsync_NullJson_ThrowsInvalidOperationException()
    {
        HttpClient httpClient = new(new NullBodyHandler());
        NullDtoAuth auth = new();
        TidalApiClient client = new(httpClient, auth);
        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetAlbumAsync("al1"));
    }

    [Fact]
    public async Task GetAlbumTracksAsync_NullJson_ThrowsInvalidOperationException()
    {
        HttpClient httpClient = new(new NullBodyHandler());
        NullDtoAuth auth = new();
        TidalApiClient client = new(httpClient, auth);
        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetAlbumTracksAsync("al1"));
    }

    [Fact]
    public async Task SearchAsync_NullJson_ThrowsInvalidOperationException()
    {
        HttpClient httpClient = new(new NullBodyHandler());
        NullDtoAuth auth = new();
        TidalApiClient client = new(httpClient, auth);
        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => client.SearchAsync("abc"));
    }

    private class NullBodyHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpResponseMessage msg = new(HttpStatusCode.OK)
            {
                Content = new StringContent("null", Encoding.UTF8, "application/json")
            };
            return Task.FromResult(msg);
        }
    }

    private class NullDtoAuth : ITidalAuth
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
}



