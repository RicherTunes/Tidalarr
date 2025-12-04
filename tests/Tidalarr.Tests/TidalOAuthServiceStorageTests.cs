using System.Text.Json;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Authentication;
using Tidalarr.Infrastructure.Storage;

namespace Tidalarr.Tests;

public class TidalOAuthServiceStorageTests
{
    private class SpyStorage : ITokenStorage
    {
        public TidalTokens? LastSaved { get; private set; }
        private TidalTokens? _tokens;
        public Task SaveTokensAsync(TidalTokens tokens) { LastSaved = tokens; this._tokens = tokens; return Task.CompletedTask; }
        public Task<TidalTokens?> LoadTokensAsync()
        {
            return Task.FromResult(this._tokens);
        }

        public Task DeleteTokensAsync() { this._tokens = null; return Task.CompletedTask; }
    }

    [Fact]
    public async Task ExchangeCodeAsync_SavesTokensToStorage()
    {
        var response = new TidalTokenResponse("atk", "rtk", "Bearer", 3600, new("sess", "US", 1));
        HttpClient http = new HttpClient(new tests_Tidalarr_Tests_Utils.RoutingHandler()
            .MapAny(JsonSerializer.Serialize(response)));
        SpyStorage storage = new SpyStorage();
        TidalOAuthService svc = new TidalOAuthService(http, storage);

        TidalTokens tokens = await svc.ExchangeCodeAsync("code", "ver");
        Assert.NotNull(storage.LastSaved);
        Assert.Equal(tokens.AccessToken, storage.LastSaved!.AccessToken);
    }
}




