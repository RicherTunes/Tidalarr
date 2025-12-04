using Tidalarr.Core.Models;
using Tidalarr.Domain.Authentication;
using Tidalarr.Infrastructure.Storage;

namespace Tidalarr.Tests;

public class TidalOAuthServiceLogoutTests
{
    private class PreloadedStorage(TidalTokens t) : ITokenStorage
    {
        private TidalTokens? _tokens = t;

        public Task SaveTokensAsync(TidalTokens tokens) { this._tokens = tokens; return Task.CompletedTask; }
        public Task<TidalTokens?> LoadTokensAsync()
        {
            return Task.FromResult(this._tokens);
        }

        public Task DeleteTokensAsync() { this._tokens = null; return Task.CompletedTask; }
    }

    [Fact]
    public async Task LogoutAsync_ClearsStoredAndCurrentTokens()
    {
        TidalTokens tokens = new TidalTokens("at", "rt", "Bearer", DateTime.UtcNow.AddHours(1), "sess", "US", "uid");
        PreloadedStorage storage = new PreloadedStorage(tokens);
        TidalOAuthService svc = new TidalOAuthService(new HttpClient(), storage);

        // Prime current tokens by loading
        TidalTokens loaded = await svc.GetValidTokensAsync();
        Assert.NotNull(loaded);

        await svc.LogoutAsync();
        _ = await Assert.ThrowsAsync<InvalidOperationException>(svc.GetValidTokensAsync);
    }
}




