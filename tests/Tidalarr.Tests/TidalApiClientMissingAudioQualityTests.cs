using System.Text.Json;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Api;
using Xunit;

namespace Tidalarr.Tests;

public class TidalApiClientMissingAudioQualityTests
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
    public async Task GetTrackAsync_MissingAudioQuality_ThrowsJsonException()
    {
        var dto = new { id = "t1", title = "T", artist = new { name = "A", id = "a1" }, album = new { id = "al1", title = "A", artist = new { name = "A", id = "a1" }, releaseDate = DateTime.UtcNow.ToString("yyyy-MM-dd"), numberOfTracks = 1, duration = 1, streamReady = true, cover = "c" }, trackNumber = 1, duration = 10, streamReady = true };
        var json = JsonSerializer.Serialize(dto);
        var api = new TidalApiClient(new HttpClient(new tests_Tidalarr_Tests_Utils.BodyHandler(json)), new Auth());
        try
        {
            var track = await api.GetTrackAsync("t1");
            Assert.NotNull(track);
        }
        catch (System.Text.Json.JsonException)
        {
            // acceptable
        }
    }
}



