using System.Net;
using System.Text;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Api;
using Xunit;

namespace Tidalarr.Tests;

public class TidalApiClientNullPlaybackTests
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

    private class NullHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent("null", Encoding.UTF8, "application/json") });
    }

    [Fact]
    public async Task GetStreamInfoAsync_NullJson_ThrowsInvalidOperation()
    {
        var api = new TidalApiClient(new HttpClient(new NullHandler()), new Auth());
        await Assert.ThrowsAsync<InvalidOperationException>(() => api.GetStreamInfoAsync("t1", TidalQuality.Lossless));
    }
}




