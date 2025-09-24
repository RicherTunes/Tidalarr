using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Api;
using Xunit;

namespace Tidalarr.Tests;

public class TidalApiClientAuthCheckTests
{
    private class ThrowAuth : ITidalAuth
    {
        public bool IsAuthenticated => false;
        public Task<TidalAuthUrl> GenerateAuthUrlAsync() => Task.FromResult(new TidalAuthUrl("","","", string.Empty));
        public Task<TidalTokens> ExchangeCodeAsync(string authCode, string codeVerifier) => Task.FromResult(new TidalTokens("","","",DateTime.UtcNow,"","",""));
        public Task<TidalTokens> RefreshTokensAsync(string refreshToken) => Task.FromResult(new TidalTokens("","","",DateTime.UtcNow,"","",""));
        public Task<TidalTokens> GetValidTokensAsync() => throw new InvalidOperationException("not authenticated");
    }

    [Fact]
    public async Task IsAuthenticatedAsync_ReturnsFalse_WhenAuthThrows()
    {
        var client = new TidalApiClient(new HttpClient(), new ThrowAuth());
        var ok = await client.IsAuthenticatedAsync();
        Assert.False(ok);
    }
}

