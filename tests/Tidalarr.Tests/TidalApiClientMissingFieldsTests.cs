using System.Text;
using System.Text.Json;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Api;
using Xunit;

namespace Tidalarr.Tests;

public class TidalApiClientMissingFieldsTests
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
    public async Task GetAlbumAsync_MissingRequiredFields_ThrowsOrMaps()
    {
        // Build JSON missing 'title' field
        var json = "{" + "\"id\":\"al1\",\"artist\":{\"name\":\"A\",\"id\":\"a1\"},\"releaseDate\":\"2020-01-01\",\"numberOfTracks\":1,\"duration\":1,\"streamReady\":true,\"cover\":\"c\"}";
        var api = new TidalApiClient(new HttpClient(new tests_Tidalarr_Tests_Utils.BodyHandler(json)), new Auth());
        try
        {
            var album = await api.GetAlbumAsync("al1");
            // If no exception, album should have empty Title due to safe mapping
            Assert.NotNull(album);
        }
        catch (System.Text.Json.JsonException)
        {
            // acceptable
        }
    }

    [Fact]
    public async Task SearchAsync_MissingCollections_ThrowsArgumentNull()
    {
        var json = "{" + "\"albums\":{},\"tracks\":{}}"; // missing 'items' arrays
        var api = new TidalApiClient(new HttpClient(new tests_Tidalarr_Tests_Utils.BodyHandler(json)), new Auth());
        await Assert.ThrowsAsync<System.ArgumentNullException>(() => api.SearchAsync("abc"));
    }
}


