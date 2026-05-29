using System.Text.Json;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Api;

namespace Tidalarr.Tests;

public class TidalApiClientAlbumTracksMissingItemsTests
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
    public async Task GetAlbumTracksAsync_MissingItems_Throws()
    {
        string json = JsonSerializer.Serialize(new { totalNumberOfItems = 1 });
        TidalApiClient api = new(new HttpClient(new tests_Tidalarr_Tests_Utils.BodyHandler(json)), new Auth());
        _ = await Assert.ThrowsAnyAsync<Exception>(() => api.GetAlbumTracksAsync("al1"));
    }

    [Fact]
    public async Task GetAlbumTracksAsync_TrackWithoutAlbum_DoesNotThrow_MapsRemainder()
    {
        // Regression (harden campaign): a single track DTO with no nested `album` threw in
        // MapToTidalTrackInfo, which runs via .Select(...) — so it discarded the ENTIRE result
        // (here the whole track list; in search, all albums/tracks/artists). It must map the
        // album-less track with empty album info instead of aborting the batch.
        string json = JsonSerializer.Serialize(new
        {
            items = new object[]
            {
                new { id = 1, title = "Has Album", trackNumber = 1, album = new { id = 10, title = "Alb" } },
                new { id = 2, title = "No Album", trackNumber = 2 } // album omitted
            }
        });
        TidalApiClient api = new(new HttpClient(new tests_Tidalarr_Tests_Utils.BodyHandler(json)), new Auth());

        var tracks = await api.GetAlbumTracksAsync("al1");

        Assert.Equal(2, tracks.Count);                 // neither track dropped, nothing thrown
        Assert.Equal("No Album", tracks[1].Title);
        Assert.Equal(string.Empty, tracks[1].AlbumId); // mapped with empty album info
    }
}



