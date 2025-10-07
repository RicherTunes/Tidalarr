using System.Text.Json;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Api;
using Xunit;

namespace Tidalarr.Tests;

public class TidalApiClientReleaseDateFallbackTests
{
    private class Auth : ITidalAuth
    {
        public bool IsAuthenticated => true;
        public Task<TidalAuthUrl> GenerateAuthUrlAsync() => Task.FromResult(new TidalAuthUrl("","","", string.Empty));
        public Task<TidalTokens> ExchangeCodeAsync(string authCode, string codeVerifier) => Task.FromResult(Default());
        public Task<TidalTokens> RefreshTokensAsync(string refreshToken) => Task.FromResult(Default());
        public Task<TidalTokens> GetValidTokensAsync() => Task.FromResult(Default());
        private static TidalTokens Default() => new("at","rt","Bearer", DateTime.UtcNow.AddHours(1), "sess","US","uid");
    }

    [Fact]
    public async Task GetAlbumAsync_InvalidReleaseDate_UsesMinValue()
    {
        var dto = new TidalAlbumDto("al1","A", new("X","a1"), "not-a-date", 1,1,true,"c");
        var http = new HttpClient(new tests_Tidalarr_Tests_Utils.BodyHandler(JsonSerializer.Serialize(dto)));
        var client = new TidalApiClient(http, new Auth());
        var album = await client.GetAlbumAsync("al1");
        Assert.Equal(DateTime.MinValue, album.ReleaseDate);
    }
}




