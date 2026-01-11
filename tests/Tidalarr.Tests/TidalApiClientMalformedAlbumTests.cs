using System.Text.Json;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Api;

namespace Tidalarr.Tests;

public class TidalApiClientMalformedAlbumTests
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

        public TidalCallbackResult ParseCallbackUrl(string callbackUrl)
        {
            return TidalCallbackResult.Failure("Not implemented in test stub");
        }

        private static TidalTokens Default()
        {
            return new("at", "rt", "Bearer", DateTime.UtcNow.AddHours(1), "sess", "US", "uid");
        }
    }

    [Fact]
    public async Task GetAlbumAsync_NullArtist_FallsBackToUnknownArtist()
    {
        var album = new { id = "al1", title = "A", artist = (object?)null, releaseDate = DateTime.UtcNow.ToString("yyyy-MM-dd"), numberOfTracks = 1, duration = 1, streamReady = true, cover = "c" };
        string json = JsonSerializer.Serialize(album);
        TidalApiClient api = new(new HttpClient(new tests_Tidalarr_Tests_Utils.BodyHandler(json)), new Auth());

        var result = await api.GetAlbumAsync("al1");

        Assert.NotNull(result);
        Assert.Single(result.Artists);
        Assert.Equal("Unknown Artist", result.Artists[0]);
    }
}



