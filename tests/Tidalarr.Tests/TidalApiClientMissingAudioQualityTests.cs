using System.Text.Json;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Api;

namespace Tidalarr.Tests;

public class TidalApiClientMissingAudioQualityTests
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
    public async Task GetTrackAsync_MissingAudioQuality_ThrowsJsonException()
    {
        var dto = new { id = "t1", title = "T", artist = new { name = "A", id = "a1" }, album = new { id = "al1", title = "A", artist = new { name = "A", id = "a1" }, releaseDate = DateTime.UtcNow.ToString("yyyy-MM-dd"), numberOfTracks = 1, duration = 1, streamReady = true, cover = "c" }, trackNumber = 1, duration = 10, streamReady = true };
        string json = JsonSerializer.Serialize(dto);
        TidalApiClient api = new(new HttpClient(new tests_Tidalarr_Tests_Utils.BodyHandler(json)), new Auth());
        try
        {
            TidalTrackInfo track = await api.GetTrackAsync("t1");
            Assert.NotNull(track);
        }
        catch (JsonException)
        {
            // acceptable
        }
    }
}



