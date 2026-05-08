using Lidarr.Plugin.Common.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Authentication;

namespace Tidalarr.Tests;

public class TidalOAuthServiceLogoutTests
{
    private class PreloadedStorage : ITokenStore<TidalTokens>
    {
        private TokenEnvelope<TidalTokens>? _envelope;

        public PreloadedStorage(TidalTokens t)
        {
            this._envelope = new TokenEnvelope<TidalTokens>(t, t.ExpiresAt);
        }

        public Task SaveAsync(TokenEnvelope<TidalTokens> envelope, CancellationToken cancellationToken = default)
        { this._envelope = envelope; return Task.CompletedTask; }

        public Task<TokenEnvelope<TidalTokens>?> LoadAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(this._envelope);
        }

        public Task ClearAsync(CancellationToken cancellationToken = default) { this._envelope = null; return Task.CompletedTask; }
    }

    [Fact]
    public async Task LogoutAsync_ClearsStoredAndCurrentTokens()
    {
        TidalTokens tokens = new("at", "rt", "Bearer", DateTime.UtcNow.AddHours(1), "sess", "US", "uid");
        PreloadedStorage storage = new(tokens);
        TidalOAuthService svc = new(new HttpClient(), storage);

        // Prime current tokens by loading
        TidalTokens loaded = await svc.GetValidTokensAsync();
        Assert.NotNull(loaded);

        await svc.LogoutAsync();
        _ = await Assert.ThrowsAsync<InvalidOperationException>(svc.GetValidTokensAsync);
    }
}




