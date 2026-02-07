using System.Net;
using System.Text;
using System.Text.Json;
using tests_Tidalarr_Tests_Utils;
using Tidalarr.Application.Services;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Api;
using Tidalarr.Domain.Quality;

namespace Tidalarr.Tests.Integration;

/// <summary>
/// Hermetic end-to-end gate tests for Tidalarr.
/// These tests exercise the full search -> track metadata -> download plan flow
/// using mocked HTTP responses via RoutingHandler. No real Tidal API calls are made.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Area", "E2E/Hermetic")]
public class E2EHermeticGateTests
{
    // ---------------------------------------------------------------------------
    // Shared helpers
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Builds a JSON search response with one album and one track.
    /// </summary>
    private static string BuildSearchResponseJson(
        string albumId = "111",
        string albumTitle = "Kind of Blue",
        string artistName = "Miles Davis",
        string trackId = "222",
        string trackTitle = "So What",
        string audioQuality = "LOSSLESS")
    {
        var response = new
        {
            albums = new
            {
                items = new[]
                {
                    new
                    {
                        id = long.Parse(albumId),
                        title = albumTitle,
                        artist = new { id = 1001L, name = artistName },
                        artists = new[] { new { id = 1001L, name = artistName } },
                        releaseDate = "1959-08-17",
                        numberOfTracks = 5,
                        streamReady = true,
                        cover = "cover-id-abc",
                        audioQuality
                    }
                },
                totalNumberOfItems = 1
            },
            tracks = new
            {
                items = new[]
                {
                    new
                    {
                        id = long.Parse(trackId),
                        title = trackTitle,
                        artist = new { id = 1001L, name = artistName },
                        artists = new[] { new { id = 1001L, name = artistName } },
                        album = new
                        {
                            id = long.Parse(albumId),
                            title = albumTitle,
                            artist = new { id = 1001L, name = artistName },
                            releaseDate = "1959-08-17",
                            numberOfTracks = 5,
                            streamReady = true,
                            cover = "cover-id-abc",
                            audioQuality
                        },
                        trackNumber = 1,
                        duration = 565,
                        streamReady = true,
                        audioQuality
                    }
                },
                totalNumberOfItems = 1
            }
        };
        return JsonSerializer.Serialize(response);
    }

    /// <summary>
    /// Builds a JSON track response.
    /// </summary>
    private static string BuildTrackResponseJson(
        string trackId = "222",
        string trackTitle = "So What",
        string artistName = "Miles Davis",
        string albumId = "111",
        string albumTitle = "Kind of Blue",
        string audioQuality = "LOSSLESS",
        int duration = 565)
    {
        var track = new
        {
            id = long.Parse(trackId),
            title = trackTitle,
            artist = new { id = 1001L, name = artistName },
            artists = new[] { new { id = 1001L, name = artistName } },
            album = new
            {
                id = long.Parse(albumId),
                title = albumTitle,
                artist = new { id = 1001L, name = artistName },
                releaseDate = "1959-08-17",
                numberOfTracks = 5,
                streamReady = true,
                cover = "cover-id-abc",
                audioQuality
            },
            trackNumber = 1,
            duration,
            streamReady = true,
            audioQuality
        };
        return JsonSerializer.Serialize(track);
    }

    /// <summary>
    /// Builds a JSON playback info (stream manifest) response.
    /// The manifest field is a base64-encoded DASH MPD stub.
    /// </summary>
    private static string BuildPlaybackInfoJson(
        string trackId = "222",
        string audioQuality = "LOSSLESS",
        bool encrypted = false)
    {
        string mpdXml = "<MPD><Period><AdaptationSet><Representation><SegmentTemplate><SegmentURL media=\"https://cdn.tidal.com/chunk1.m4a\"/></SegmentTemplate></Representation></AdaptationSet></Period></MPD>";
        string manifestB64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(mpdXml));

        var dto = new
        {
            trackId = long.Parse(trackId),
            assetPresentation = "FULL",
            audioQuality,
            audioMode = "STEREO",
            manifestMimeType = "application/dash+xml",
            manifest = manifestB64,
            encryptionType = encrypted ? "AES128" : "NONE",
            securityToken = encrypted ? "sec-tok-123" : (string?)null,
            bitDepth = 16,
            sampleRate = 44100
        };
        return JsonSerializer.Serialize(dto);
    }

    /// <summary>
    /// ITidalAuth stub that returns valid tokens for the golden path.
    /// </summary>
    private class ValidAuth : ITidalAuth
    {
        public bool IsAuthenticated => true;

        public Task<TidalAuthUrl> GenerateAuthUrlAsync()
            => Task.FromResult(new TidalAuthUrl("https://login.tidal.com", "verifier", "state123", "cuk"));

        public Task<TidalTokens> ExchangeCodeAsync(string authCode, string codeVerifier)
            => Task.FromResult(MakeTokens());

        public Task<TidalTokens> RefreshTokensAsync(string refreshToken)
            => Task.FromResult(MakeTokens());

        public Task<TidalTokens> GetValidTokensAsync()
            => Task.FromResult(MakeTokens());

        public TidalCallbackResult ParseCallbackUrl(string callbackUrl)
            => TidalCallbackResult.Failure("Not implemented in test stub");

        private static TidalTokens MakeTokens()
            => new("valid-access-token", "valid-refresh-token", "Bearer", DateTime.UtcNow.AddHours(1), "sess-42", "US", "user-1");
    }

    /// <summary>
    /// ITidalAuth stub that simulates an expired/invalid OAuth token by throwing on GetValidTokensAsync.
    /// </summary>
    private class ExpiredAuth : ITidalAuth
    {
        public bool IsAuthenticated => false;

        public Task<TidalAuthUrl> GenerateAuthUrlAsync()
            => Task.FromResult(new TidalAuthUrl("https://login.tidal.com", "verifier", "state123", "cuk"));

        public Task<TidalTokens> ExchangeCodeAsync(string authCode, string codeVerifier)
            => throw new InvalidOperationException("Token exchange failed: auth code expired");

        public Task<TidalTokens> RefreshTokensAsync(string refreshToken)
            => throw new InvalidOperationException("Refresh token expired or revoked");

        public Task<TidalTokens> GetValidTokensAsync()
            => throw new InvalidOperationException("OAuth session expired. Please re-authenticate.");

        public TidalCallbackResult ParseCallbackUrl(string callbackUrl)
            => TidalCallbackResult.Failure("Not implemented in test stub");
    }

    // ---------------------------------------------------------------------------
    // Golden path: Search -> Track metadata -> Download plan construction
    // ---------------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Area", "E2E/Hermetic")]
    public async Task GoldenPath_SearchReturnsAlbumsAndTracks()
    {
        // Arrange: mock HTTP to respond to /search with a search result
        RoutingHandler handler = new RoutingHandler()
            .MapPath("/v1/search", BuildSearchResponseJson());

        HttpClient httpClient = new(handler) { BaseAddress = new Uri("https://api.tidal.com") };
        TidalApiClient apiClient = new(httpClient, new ValidAuth());

        // Act
        TidalSearchResults results = await apiClient.SearchAsync("Miles Davis Kind of Blue");

        // Assert
        Assert.NotNull(results);
        Assert.Single(results.Albums);
        Assert.Single(results.Tracks);
        Assert.Equal("Kind of Blue", results.Albums[0].Title);
        Assert.Equal("So What", results.Tracks[0].Title);
        Assert.Contains("Miles Davis", results.Albums[0].Artists);
        Assert.Contains("Miles Davis", results.Tracks[0].Artists);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Area", "E2E/Hermetic")]
    public async Task GoldenPath_TrackMetadataPopulatedCorrectly()
    {
        // Arrange: mock the track endpoint
        RoutingHandler handler = new RoutingHandler()
            .MapPath("/v1/tracks/222", BuildTrackResponseJson());

        HttpClient httpClient = new(handler) { BaseAddress = new Uri("https://api.tidal.com") };
        TidalApiClient apiClient = new(httpClient, new ValidAuth());

        // Act
        TidalTrackInfo track = await apiClient.GetTrackAsync("222");

        // Assert
        Assert.Equal("222", track.Id);
        Assert.Equal("So What", track.Title);
        Assert.Equal(565, track.Duration);
        Assert.Equal(1, track.TrackNumber);
        Assert.Equal(TidalQuality.Lossless, track.Quality);
        Assert.True(track.IsAvailable);
        Assert.Equal("111", track.AlbumId);
        Assert.Equal("Kind of Blue", track.AlbumTitle);
        Assert.Contains("Miles Davis", track.Artists);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Area", "E2E/Hermetic")]
    public async Task GoldenPath_StreamInfoReturnsPlaybackManifest()
    {
        // Arrange: mock the playback info endpoint
        RoutingHandler handler = new RoutingHandler()
            .MapPath("/v1/tracks/222/playbackinfopostpaywall", BuildPlaybackInfoJson());

        HttpClient httpClient = new(handler) { BaseAddress = new Uri("https://api.tidal.com") };
        TidalApiClient apiClient = new(httpClient, new ValidAuth());

        // Act
        TidalStreamInfo streamInfo = await apiClient.GetStreamInfoAsync("222", TidalQuality.Lossless);

        // Assert
        Assert.Equal("222", streamInfo.TrackId);
        Assert.False(streamInfo.IsEncrypted);
        Assert.Null(streamInfo.SecurityToken);
        // The mime type should come from the playback info
        Assert.False(string.IsNullOrEmpty(streamInfo.MimeType));
        // File extension should be inferred
        Assert.False(string.IsNullOrEmpty(streamInfo.FileExtension));
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Area", "E2E/Hermetic")]
    public async Task GoldenPath_FullFlow_SearchThenTrackThenStream()
    {
        // Arrange: wire up all three endpoints into one RoutingHandler
        RoutingHandler handler = new RoutingHandler()
            .MapPath("/v1/search", BuildSearchResponseJson())
            .MapPath("/v1/tracks/222/playbackinfopostpaywall", BuildPlaybackInfoJson())
            .MapPath("/v1/tracks/222", BuildTrackResponseJson());

        HttpClient httpClient = new(handler) { BaseAddress = new Uri("https://api.tidal.com") };
        TidalApiClient apiClient = new(httpClient, new ValidAuth());

        // Act: Step 1 - Search
        TidalSearchResults searchResults = await apiClient.SearchAsync("Miles Davis Kind of Blue");
        Assert.NotEmpty(searchResults.Tracks);
        string foundTrackId = searchResults.Tracks[0].Id;

        // Act: Step 2 - Get track metadata
        TidalTrackInfo trackInfo = await apiClient.GetTrackAsync(foundTrackId);
        Assert.Equal("So What", trackInfo.Title);
        Assert.Equal(TidalQuality.Lossless, trackInfo.Quality);

        // Act: Step 3 - Get stream info (download plan construction)
        TidalStreamInfo streamInfo = await apiClient.GetStreamInfoAsync(foundTrackId, TidalQuality.Lossless);
        Assert.Equal(foundTrackId, streamInfo.TrackId);
        Assert.False(streamInfo.IsEncrypted);

        // The full pipeline completed: search -> metadata -> stream plan
        Assert.True(true, "Full golden path completed: search -> track metadata -> stream info");
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Area", "E2E/Hermetic")]
    public async Task GoldenPath_SearchWithQualityDetection_EnhancesResults()
    {
        // Arrange: search endpoint returns albums with LOSSLESS quality
        RoutingHandler handler = new RoutingHandler()
            .MapPath("/v1/search", BuildSearchResponseJson(audioQuality: "LOSSLESS"));

        HttpClient httpClient = new(handler) { BaseAddress = new Uri("https://api.tidal.com") };
        TidalApiClient apiClient = new(httpClient, new ValidAuth());
        TidalSearchService searchService = new(apiClient, new TidalQualityDetector());

        // Act
        TidalSearchResults results = await searchService.SearchWithQualityDetectionAsync("Miles Davis", TidalQuality.Lossless);

        // Assert: albums should have quality info preserved from API
        Assert.Single(results.Albums);
        TidalAlbumInfo album = results.Albums[0];
        Assert.NotEmpty(album.AvailableQualities);
        // LOSSLESS means Low, High, and Lossless should all be available (all <= Lossless)
        Assert.Contains(TidalQuality.Lossless, album.AvailableQualities);
        Assert.Contains(TidalQuality.High, album.AvailableQualities);
        Assert.Contains(TidalQuality.Low, album.AvailableQualities);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Area", "E2E/Hermetic")]
    public async Task GoldenPath_EncryptedStream_ReportsEncryptionState()
    {
        // Arrange: playback info indicates an encrypted stream
        RoutingHandler handler = new RoutingHandler()
            .MapPath("/v1/tracks/333/playbackinfopostpaywall", BuildPlaybackInfoJson(trackId: "333", encrypted: true));

        HttpClient httpClient = new(handler) { BaseAddress = new Uri("https://api.tidal.com") };
        TidalApiClient apiClient = new(httpClient, new ValidAuth());

        // Act
        TidalStreamInfo streamInfo = await apiClient.GetStreamInfoAsync("333", TidalQuality.Lossless);

        // Assert
        Assert.True(streamInfo.IsEncrypted);
        Assert.Equal("sec-tok-123", streamInfo.SecurityToken);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Area", "E2E/Hermetic")]
    public async Task GoldenPath_HiResQuality_DetectedCorrectly()
    {
        // Arrange: search returns HI_RES_LOSSLESS quality
        RoutingHandler handler = new RoutingHandler()
            .MapPath("/v1/search", BuildSearchResponseJson(audioQuality: "HI_RES_LOSSLESS"));

        HttpClient httpClient = new(handler) { BaseAddress = new Uri("https://api.tidal.com") };
        TidalApiClient apiClient = new(httpClient, new ValidAuth());

        // Act
        TidalSearchResults results = await apiClient.SearchAsync("high-res test");

        // Assert: track and album should report HiRes quality
        Assert.Single(results.Tracks);
        Assert.Equal(TidalQuality.HiRes, results.Tracks[0].Quality);
        Assert.Contains(TidalQuality.HiRes, results.Albums[0].AvailableQualities);
    }

    // ---------------------------------------------------------------------------
    // Auth-fail path: Expired OAuth token -> graceful error, no crash
    // ---------------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Area", "E2E/Hermetic")]
    public async Task AuthFail_SearchWithExpiredToken_ThrowsInvalidOperationException()
    {
        // Arrange: expired auth will throw on GetValidTokensAsync
        RoutingHandler handler = new RoutingHandler()
            .MapAny("""{"error": "unauthorized"}""", HttpStatusCode.Unauthorized);

        HttpClient httpClient = new(handler) { BaseAddress = new Uri("https://api.tidal.com") };
        TidalApiClient apiClient = new(httpClient, new ExpiredAuth());

        // Act & Assert: SearchAsync should propagate the auth exception
        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => apiClient.SearchAsync("Miles Davis"));

        Assert.Contains("expired", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Area", "E2E/Hermetic")]
    public async Task AuthFail_GetTrackWithExpiredToken_ThrowsInvalidOperationException()
    {
        // Arrange: expired auth
        RoutingHandler handler = new RoutingHandler()
            .MapAny("""{"error": "unauthorized"}""", HttpStatusCode.Unauthorized);

        HttpClient httpClient = new(handler) { BaseAddress = new Uri("https://api.tidal.com") };
        TidalApiClient apiClient = new(httpClient, new ExpiredAuth());

        // Act & Assert: GetTrackAsync should propagate the auth exception
        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => apiClient.GetTrackAsync("222"));

        Assert.Contains("expired", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Area", "E2E/Hermetic")]
    public async Task AuthFail_GetStreamInfoWithExpiredToken_ThrowsInvalidOperationException()
    {
        // Arrange: expired auth
        RoutingHandler handler = new RoutingHandler()
            .MapAny("""{"error": "unauthorized"}""", HttpStatusCode.Unauthorized);

        HttpClient httpClient = new(handler) { BaseAddress = new Uri("https://api.tidal.com") };
        TidalApiClient apiClient = new(httpClient, new ExpiredAuth());

        // Act & Assert: GetStreamInfoAsync should propagate the auth exception
        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => apiClient.GetStreamInfoAsync("222", TidalQuality.Lossless));

        Assert.Contains("expired", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Area", "E2E/Hermetic")]
    public async Task AuthFail_IsAuthenticatedAsync_ReturnsFalseGracefully()
    {
        // Arrange: expired auth - IsAuthenticatedAsync should NOT throw, it should return false
        RoutingHandler handler = new RoutingHandler()
            .MapAny("""{"error": "unauthorized"}""", HttpStatusCode.Unauthorized);

        HttpClient httpClient = new(handler) { BaseAddress = new Uri("https://api.tidal.com") };
        TidalApiClient apiClient = new(httpClient, new ExpiredAuth());

        // Act: this should not throw
        bool isAuthenticated = await apiClient.IsAuthenticatedAsync();

        // Assert: graceful false, no crash
        Assert.False(isAuthenticated);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Area", "E2E/Hermetic")]
    public async Task AuthFail_SearchService_ReturnsEmptyResultsGracefully()
    {
        // Arrange: expired auth with SearchByTypeAsync (uses SafeOperationExecutor)
        RoutingHandler handler = new RoutingHandler()
            .MapAny("""{"error": "unauthorized"}""", HttpStatusCode.Unauthorized);

        HttpClient httpClient = new(handler) { BaseAddress = new Uri("https://api.tidal.com") };
        TidalApiClient apiClient = new(httpClient, new ExpiredAuth());
        TidalSearchService searchService = new(apiClient, new TidalQualityDetector());

        // Act: SearchByTypeAsync wraps with SafeOperationExecutor, should not throw
        TidalSearchResults results = await searchService.SearchByTypeAsync("Miles Davis", TidalSearchType.Album);

        // Assert: graceful empty results, no crash
        Assert.NotNull(results);
        Assert.Empty(results.Albums);
        Assert.Empty(results.Tracks);
        Assert.Equal(0, results.TotalCount);
    }

    // ---------------------------------------------------------------------------
    // Edge cases
    // ---------------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Area", "E2E/Hermetic")]
    public async Task Edge_HttpServerError_ThrowsHttpRequestException()
    {
        // Arrange: server returns 500
        RoutingHandler handler = new RoutingHandler()
            .MapAny("""{"error": "internal server error"}""", HttpStatusCode.InternalServerError);

        HttpClient httpClient = new(handler) { BaseAddress = new Uri("https://api.tidal.com") };
        TidalApiClient apiClient = new(httpClient, new ValidAuth());

        // Act & Assert: EnsureSuccessStatusCode will throw HttpRequestException
        await Assert.ThrowsAsync<HttpRequestException>(
            () => apiClient.SearchAsync("anything"));
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Area", "E2E/Hermetic")]
    public async Task Edge_Http401_WithValidAuth_ThrowsHttpRequestException()
    {
        // Arrange: auth succeeds (returns valid tokens) but API rejects with 401 (e.g. token revoked server-side)
        RoutingHandler handler = new RoutingHandler()
            .MapAny("""{"status":401,"subStatus":6001,"error":"Token has been revoked"}""", HttpStatusCode.Unauthorized);

        HttpClient httpClient = new(handler) { BaseAddress = new Uri("https://api.tidal.com") };
        TidalApiClient apiClient = new(httpClient, new ValidAuth());

        // Act & Assert: even with locally valid auth, server 401 should propagate
        await Assert.ThrowsAsync<HttpRequestException>(
            () => apiClient.GetTrackAsync("222"));
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Area", "E2E/Hermetic")]
    public async Task Edge_EmptySearchResults_HandledGracefully()
    {
        // Arrange: search returns empty results
        string emptySearch = JsonSerializer.Serialize(new
        {
            albums = new { items = Array.Empty<object>(), totalNumberOfItems = 0 },
            tracks = new { items = Array.Empty<object>(), totalNumberOfItems = 0 }
        });

        RoutingHandler handler = new RoutingHandler()
            .MapPath("/v1/search", emptySearch);

        HttpClient httpClient = new(handler) { BaseAddress = new Uri("https://api.tidal.com") };
        TidalApiClient apiClient = new(httpClient, new ValidAuth());

        // Act
        TidalSearchResults results = await apiClient.SearchAsync("nonexistent artist xyz");

        // Assert
        Assert.NotNull(results);
        Assert.Empty(results.Albums);
        Assert.Empty(results.Tracks);
        Assert.Equal(0, results.TotalCount);
    }
}
