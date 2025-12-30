using System.Text.Json;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Api;

namespace Tidalarr.Tests;

public class TidalApiClientReleaseDateFallbackTests
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
    public async Task GetAlbumAsync_InvalidReleaseDate_UsesMinValue()
    {
        TidalAlbumDto dto = new("al1", "A", new("X", "a1"), "not-a-date", 1, 1, true, "c");
        HttpClient http = new(new tests_Tidalarr_Tests_Utils.BodyHandler(JsonSerializer.Serialize(dto)));
        TidalApiClient client = new(http, new Auth());
        TidalAlbumInfo album = await client.GetAlbumAsync("al1");
        Assert.Equal(DateTime.MinValue, album.ReleaseDate);
    }
}



