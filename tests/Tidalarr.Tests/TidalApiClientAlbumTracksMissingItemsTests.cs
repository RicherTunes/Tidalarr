using System.Text.Json;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Api;
using Xunit;

namespace Tidalarr.Tests;

public class TidalApiClientAlbumTracksMissingItemsTests
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

    [Fact]
    public async Task GetAlbumTracksAsync_MissingItems_Throws()
    {
        var json = JsonSerializer.Serialize(new { totalNumberOfItems = 1 });
        var api = new TidalApiClient(new HttpClient(new tests_Tidalarr_Tests_Utils.BodyHandler(json)), new Auth());
        await Assert.ThrowsAnyAsync<Exception>(() => api.GetAlbumTracksAsync("al1"));
    }
}




