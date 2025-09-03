using Tidalarr.Core.Models;
using Tidalarr.Domain.Authentication;
using Tidalarr.Infrastructure.Storage;
using Xunit;

namespace Tidalarr.Tests;

public class TidalOAuthServiceLogoutTests
{
    private class PreloadedStorage : ITokenStorage
    {
        private TidalTokens? _tokens;
        public PreloadedStorage(TidalTokens t) { _tokens = t; }
        public Task SaveTokensAsync(TidalTokens tokens) { _tokens = tokens; return Task.CompletedTask; }
        public Task<TidalTokens?> LoadTokensAsync() => Task.FromResult(_tokens);
        public Task DeleteTokensAsync() { _tokens = null; return Task.CompletedTask; }
    }

    [Fact]
    public async Task LogoutAsync_ClearsStoredAndCurrentTokens()
    {
        var tokens = new TidalTokens("at","rt","Bearer", DateTime.UtcNow.AddHours(1), "sess","US","uid");
        var storage = new PreloadedStorage(tokens);
        var svc = new TidalOAuthService(new HttpClient(), storage);

        // Prime current tokens by loading
        var loaded = await svc.GetValidTokensAsync();
        Assert.NotNull(loaded);

        await svc.LogoutAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.GetValidTokensAsync());
    }
}

