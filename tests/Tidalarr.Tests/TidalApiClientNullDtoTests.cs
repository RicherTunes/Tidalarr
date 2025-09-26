using System.Net;
using System.Text;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Api;
using Xunit;

namespace Tidalarr.Tests;

public class TidalApiClientNullDtoTests
{
    [Fact]
    public async Task GetAlbumAsync_NullJson_ThrowsInvalidOperationException()
    {
        var httpClient = new HttpClient(new NullBodyHandler());
        var auth = new NullDtoAuth();
        var client = new TidalApiClient(httpClient, auth);
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetAlbumAsync("al1"));
    }

    [Fact]
    public async Task GetAlbumTracksAsync_NullJson_ThrowsInvalidOperationException()
    {
        var httpClient = new HttpClient(new NullBodyHandler());
        var auth = new NullDtoAuth();
        var client = new TidalApiClient(httpClient, auth);
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetAlbumTracksAsync("al1"));
    }

    [Fact]
    public async Task SearchAsync_NullJson_ThrowsInvalidOperationException()
    {
        var httpClient = new HttpClient(new NullBodyHandler());
        var auth = new NullDtoAuth();
        var client = new TidalApiClient(httpClient, auth);
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.SearchAsync("abc"));
    }

    private class NullBodyHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var msg = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("null", Encoding.UTF8, "application/json")
            };
            return Task.FromResult(msg);
        }
    }

    private class NullDtoAuth : ITidalAuth
    {
        public bool IsAuthenticated => true;
        public Task<TidalAuthUrl> GenerateAuthUrlAsync() => Task.FromResult(new TidalAuthUrl("","","", string.Empty));
        public Task<TidalTokens> ExchangeCodeAsync(string authCode, string codeVerifier) => Task.FromResult(Default());
        public Task<TidalTokens> RefreshTokensAsync(string refreshToken) => Task.FromResult(Default());
        public Task<TidalTokens> GetValidTokensAsync() => Task.FromResult(Default());
        private static TidalTokens Default() => new("at","rt","Bearer", DateTime.UtcNow.AddHours(1), "sess","US","uid");
    }
}



