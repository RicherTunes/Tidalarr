using System.Text.Json;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Api;

namespace Tidalarr.Tests;

public class TidalApiClientMalformedTrackTests
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
    public async Task GetTrackAsync_NullAlbum_Throws()
    {
        var track = new { id = "t1", title = "T", artist = new { name = "A", id = "a1" }, album = (object?)null, trackNumber = 1, duration = 10, streamReady = true, audioQuality = "LOSSLESS" };
        string json = JsonSerializer.Serialize(track);
        TidalApiClient api = new(new HttpClient(new tests_Tidalarr_Tests_Utils.BodyHandler(json)), new Auth());
        _ = await Assert.ThrowsAnyAsync<Exception>(() => api.GetTrackAsync("t1"));
    }
}




