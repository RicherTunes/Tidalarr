using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Api;

namespace Tidalarr.Tests;

public class TidalApiClientAuthCheckTests
{
    private class ThrowAuth : ITidalAuth
    {
        public bool IsAuthenticated => false;
        public Task<TidalAuthUrl> GenerateAuthUrlAsync()
        {
            return Task.FromResult(new TidalAuthUrl("", "", "", string.Empty));
        }

        public Task<TidalTokens> ExchangeCodeAsync(string authCode, string codeVerifier)
        {
            return Task.FromResult(new TidalTokens("", "", "", DateTime.UtcNow, "", "", ""));
        }

        public Task<TidalTokens> RefreshTokensAsync(string refreshToken)
        {
            return Task.FromResult(new TidalTokens("", "", "", DateTime.UtcNow, "", "", ""));
        }

        public Task<TidalTokens> GetValidTokensAsync()
        {
            throw new InvalidOperationException("not authenticated");
        }
    }

    [Fact]
    public async Task IsAuthenticatedAsync_ReturnsFalse_WhenAuthThrows()
    {
        TidalApiClient client = new(new HttpClient(), new ThrowAuth());
        bool ok = await client.IsAuthenticatedAsync();
        Assert.False(ok);
    }
}




