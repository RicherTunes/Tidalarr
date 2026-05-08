using System.Text.Json;
using Lidarr.Plugin.Common.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Authentication;

namespace Tidalarr.Tests;

public class TidalOAuthServiceStorageTests
{
    private class SpyStorage : ITokenStore<TidalTokens>
    {
        public TidalTokens? LastSaved { get; private set; }
        private TokenEnvelope<TidalTokens>? _envelope;

        public Task SaveAsync(TokenEnvelope<TidalTokens> envelope, CancellationToken cancellationToken = default)
        { LastSaved = envelope.Session; this._envelope = envelope; return Task.CompletedTask; }

        public Task<TokenEnvelope<TidalTokens>?> LoadAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(this._envelope);
        }

        public Task ClearAsync(CancellationToken cancellationToken = default) { this._envelope = null; return Task.CompletedTask; }
    }

    [Fact]
    public async Task ExchangeCodeAsync_SavesTokensToStorage()
    {
        Domain.Authentication.TidalTokenResponse response = new("atk", "rtk", "Bearer", 3600, new("sess", "US", 1));
        HttpClient http = new(new tests_Tidalarr_Tests_Utils.RoutingHandler()
            .MapAny(JsonSerializer.Serialize(response)));
        SpyStorage storage = new();
        TidalOAuthService svc = new(http, storage);

        TidalTokens tokens = await svc.ExchangeCodeAsync("code", "ver");
        Assert.NotNull(storage.LastSaved);
        Assert.Equal(tokens.AccessToken, storage.LastSaved!.AccessToken);
    }
}




