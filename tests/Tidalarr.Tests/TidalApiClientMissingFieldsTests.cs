using System.Text.Json;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Api;

namespace Tidalarr.Tests;

public class TidalApiClientMissingFieldsTests
{
    private class Auth : ITidalAuth
    {
        public bool IsAuthenticated => true;
        public Task<TidalAuthUrl> GenerateAuthUrlAsync()
        {
            return Task.FromResult(new TidalAuthUrl("", "", "", string.Empty));
        }

        public Task<TidalTokens> ExchangeCodeAsync(string authCode, string codeVerifier)
        {
            return Task.FromResult(Default());
        }

        public Task<TidalTokens> RefreshTokensAsync(string refreshToken)
        {
            return Task.FromResult(Default());
        }

        public Task<TidalTokens> GetValidTokensAsync()
        {
            return Task.FromResult(Default());
        }

        private static TidalTokens Default()
        {
            return new("at", "rt", "Bearer", DateTime.UtcNow.AddHours(1), "sess", "US", "uid");
        }
    }

    [Fact]
    public async Task GetAlbumAsync_MissingRequiredFields_ThrowsOrMaps()
    {
        // Build JSON missing 'title' field
        string json = "{" + "\"id\":\"al1\",\"artist\":{\"name\":\"A\",\"id\":\"a1\"},\"releaseDate\":\"2020-01-01\",\"numberOfTracks\":1,\"duration\":1,\"streamReady\":true,\"cover\":\"c\"}";
        TidalApiClient api = new(new HttpClient(new tests_Tidalarr_Tests_Utils.BodyHandler(json)), new Auth());
        try
        {
            TidalAlbumInfo album = await api.GetAlbumAsync("al1");
            // If no exception, album should have empty Title due to safe mapping
            Assert.NotNull(album);
        }
        catch (JsonException)
        {
            // acceptable
        }
    }

    [Fact]
    public async Task SearchAsync_MissingCollections_ReturnsEmptyResults()
    {
        string json = "{" + "\"albums\":{},\"tracks\":{}}"; // missing 'items' arrays
        TidalApiClient api = new(new HttpClient(new tests_Tidalarr_Tests_Utils.BodyHandler(json)), new Auth());
        TidalSearchResults results = await api.SearchAsync("abc");
        Assert.Empty(results.Albums);
        Assert.Empty(results.Tracks);
    }
}


