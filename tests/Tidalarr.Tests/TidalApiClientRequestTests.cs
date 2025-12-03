using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Api;
using Xunit;

namespace Tidalarr.Tests;

public class TidalApiClientRequestTests
{
    [Fact]
    public async Task GetTrackAsync_BuildsExpectedEndpointAndQuery()
    {
        var capture = new CaptureHandler(JsonSerializer.Serialize(new TidalTrackDto
        {
            id = "t1",
            title = "T",
            artist = new TidalArtistDto { name = "A", id = "a1" },
            album = new TidalAlbumDto
            {
                id = "al1",
                title = "Alb",
                artist = new TidalArtistDto { name = "A", id = "a1" },
                releaseDate = DateTime.UtcNow.ToString("yyyy-MM-dd"),
                numberOfTracks = 1,
                streamReady = true,
                cover = "c"
            },
            trackNumber = 1,
            duration = 10,
            streamReady = true,
            audioQuality = "LOSSLESS"
        }));
        var client = new TidalApiClient(new HttpClient(capture), new RequestAuth());
        var _ = await client.GetTrackAsync("t1");
        Assert.Contains("/tracks/t1", capture.LastRequest?.RequestUri?.AbsoluteUri);
        Assert.Contains("countryCode=US", capture.LastRequest?.RequestUri?.Query);
        Assert.Contains("sessionId=sess", capture.LastRequest?.RequestUri?.Query);
    }

    [Fact]
    public async Task GetAlbumAsync_BuildsExpectedEndpointAndQuery()
    {
        var dto = new TidalAlbumDto
        {
            id = "al1",
            title = "Alb",
            artist = new TidalArtistDto { name = "A", id = "a1" },
            releaseDate = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            numberOfTracks = 1,
            streamReady = true,
            cover = "c"
        };
        var capture = new CaptureHandler(JsonSerializer.Serialize(dto));
        var client = new TidalApiClient(new HttpClient(capture), new RequestAuth());
        var _ = await client.GetAlbumAsync("al1");
        Assert.Contains("/albums/al1", capture.LastRequest?.RequestUri?.AbsoluteUri);
        Assert.Contains("countryCode=US", capture.LastRequest?.RequestUri?.Query);
    }

    [Fact]
    public async Task GetAlbumTracksAsync_BuildsExpectedEndpointAndQuery()
    {
        var payload = new TidalAlbumTracksDto
        {
            items = new List<TidalTrackDto>
            {
                new TidalTrackDto
                {
                    id = "t",
                    title = "T",
                    artist = new TidalArtistDto { name = "A", id = "a1" },
                    album = new TidalAlbumDto
                    {
                        id = "al1",
                        title = "Alb",
                        artist = new TidalArtistDto { name = "A", id = "a1" },
                        releaseDate = DateTime.UtcNow.ToString("yyyy-MM-dd"),
                        numberOfTracks = 1,
                        streamReady = true,
                        cover = "c"
                    },
                    trackNumber = 1,
                    duration = 10,
                    streamReady = true,
                    audioQuality = "LOSSLESS"
                }
            },
            totalNumberOfItems = 1
        };
        var capture = new CaptureHandler(JsonSerializer.Serialize(payload));
        var client = new TidalApiClient(new HttpClient(capture), new RequestAuth());
        var _ = await client.GetAlbumTracksAsync("al1");
        Assert.Contains("/albums/al1/tracks", capture.LastRequest?.RequestUri?.AbsoluteUri);
        Assert.Contains("limit=1000", capture.LastRequest?.RequestUri?.Query);
    }

    [Fact]
    public async Task SearchAsync_BuildsExpectedEndpointAndQuery()
    {
        var payload = new TidalSearchResponseDto
        {
            albums = new TidalPagedItemsDto<TidalAlbumDto> { items = new List<TidalAlbumDto>() },
            tracks = new TidalPagedItemsDto<TidalTrackDto> { items = new List<TidalTrackDto>() }
        };
        var capture = new CaptureHandler(JsonSerializer.Serialize(payload));
        var client = new TidalApiClient(new HttpClient(capture), new RequestAuth());
        var _ = await client.SearchAsync("abc");
        Assert.Contains("/search", capture.LastRequest?.RequestUri?.AbsoluteUri);
        Assert.Contains("query=abc", capture.LastRequest?.RequestUri?.Query);
        Assert.Contains("types=albums%2Ctracks", capture.LastRequest?.RequestUri?.Query);
    }

    [Fact]
    public async Task GetStreamInfoAsync_BuildsExpectedEndpoint_AndParams()
    {
        var dto = new TidalPlaybackInfoDto
        {
            manifest = Convert.ToBase64String(Encoding.UTF8.GetBytes("<MPD/>")),
            manifestMimeType = "application/dash+xml",
            encryptionType = "NONE",
            securityToken = null
        };
        var capture = new CaptureHandler(JsonSerializer.Serialize(dto));
        var client = new TidalApiClient(new HttpClient(capture), new RequestAuth());
        var _ = await client.GetStreamInfoAsync("t1", TidalQuality.Lossless);
        Assert.Contains("/tracks/t1/playbackinfopostpaywall", capture.LastRequest?.RequestUri?.AbsoluteUri);
        Assert.Contains("audioquality=LOSSLESS", capture.LastRequest?.RequestUri?.Query);
        Assert.Contains("playbackmode=STREAM", capture.LastRequest?.RequestUri?.Query);
        Assert.Contains("assetpresentation=FULL", capture.LastRequest?.RequestUri?.Query);
    }

    private class RequestAuth : ITidalAuth
    {
        public bool IsAuthenticated => true;
        public Task<TidalAuthUrl> GenerateAuthUrlAsync() => Task.FromResult(new TidalAuthUrl("", "", "", string.Empty));
        public Task<TidalTokens> ExchangeCodeAsync(string authCode, string codeVerifier) => Task.FromResult(Default());
        public Task<TidalTokens> RefreshTokensAsync(string refreshToken) => Task.FromResult(Default());
        public Task<TidalTokens> GetValidTokensAsync() => Task.FromResult(Default());
        private static TidalTokens Default() => new("at", "rt", "Bearer", DateTime.UtcNow.AddHours(1), "sess", "US", "uid");
    }

    private class CaptureHandler : HttpMessageHandler
    {
        private readonly string _response;
        private readonly HttpStatusCode _code;
        public HttpRequestMessage? LastRequest { get; private set; }
        public CaptureHandler(string response, HttpStatusCode code = HttpStatusCode.OK)
        {
            _response = response;
            _code = code;
        }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
        {
            LastRequest = request;
            var msg = new HttpResponseMessage(_code)
            {
                Content = new StringContent(_response, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(msg);
        }
    }
}
