using System.Net;
using System.Text.Json;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Authentication;
using Tidalarr.Infrastructure.Storage;
using Xunit;

namespace Tidalarr.Tests;

public class TidalOAuthServiceStorageTests
{
    private class SpyStorage : ITokenStorage
    {
        public TidalTokens? LastSaved { get; private set; }
        private TidalTokens? _tokens;
        public Task SaveTokensAsync(TidalTokens tokens) { LastSaved = tokens; _tokens = tokens; return Task.CompletedTask; }
        public Task<TidalTokens?> LoadTokensAsync() => Task.FromResult(_tokens);
        public Task DeleteTokensAsync() { _tokens = null; return Task.CompletedTask; }
    }

    [Fact]
    public async Task ExchangeCodeAsync_SavesTokensToStorage()
    {
        var response = new Tidalarr.Domain.Authentication.TidalTokenResponse("atk", "rtk", "Bearer", 3600, new Tidalarr.Domain.Authentication.TidalUserResponse("sess", "US", 1));
        var http = new HttpClient(new tests_Tidalarr_Tests_Utils.RoutingHandler()
            .MapAny(JsonSerializer.Serialize(response)));
        var storage = new SpyStorage();
        var svc = new TidalOAuthService(http, storage);

        var tokens = await svc.ExchangeCodeAsync("code", "ver");
        Assert.NotNull(storage.LastSaved);
        Assert.Equal(tokens.AccessToken, storage.LastSaved!.AccessToken);
    }
}




