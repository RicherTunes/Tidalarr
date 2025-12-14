using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Streaming;

namespace Tidalarr.Tests.Unit;

/// <summary>
/// 100% Coverage: TidalStreamService testing
/// Tests stream coordination, validation, and quality detection
/// </summary>
public class TidalStreamServiceTests
{
    private readonly TidalStreamService _streamService;
    private readonly MockTidalApiClient _mockApiClient;
    private readonly TidalManifestParser _manifestParser;

    public TidalStreamServiceTests()
    {
        this._mockApiClient = new MockTidalApiClient();
        this._manifestParser = new TidalManifestParser();
        this._streamService = new TidalStreamService(this._mockApiClient, this._manifestParser);
    }

    [Fact]
    public async Task TidalStreamService_GetStreamInfo_WithValidTrack_ReturnsStreamInfo()
    {
        // Arrange
        this._mockApiClient.SetupStreamInfo("track123", new TidalStreamInfo(
            "track123", ["url1", "url2"], ".flac", "audio/flac", false, null));

        // Act
        TidalStreamInfo result = await this._streamService.GetStreamInfoAsync("track123", TidalQuality.Lossless);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("track123", result.TrackId);
        Assert.Equal(2, result.ChunkUrls.Length);
    }

    [Fact]
    public async Task TidalStreamService_ValidateStreamAvailability_WithValidStream_ReturnsTrue()
    {
        // Arrange
        this._mockApiClient.SetupStreamInfo("valid_track", new TidalStreamInfo(
            "valid_track", ["url1"], ".flac", "audio/flac", false, null));

        // Act
        bool result = await this._streamService.ValidateStreamAvailabilityAsync("valid_track", TidalQuality.High);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task TidalStreamService_ValidateStreamAvailability_WithInvalidStream_ReturnsFalse()
    {
        // Arrange
        this._mockApiClient.SetupThrowException("invalid_track");

        // Act
        bool result = await this._streamService.ValidateStreamAvailabilityAsync("invalid_track", TidalQuality.High);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task TidalStreamService_GetAvailableQualities_WithMultipleQualities_ReturnsAll()
    {
        // Arrange
        this._mockApiClient.SetupMultipleQualities("track123",
            TidalQuality.High, TidalQuality.Lossless);

        // Act
        List<TidalQuality> qualities = await this._streamService.GetAvailableQualitiesForTrackAsync("track123");

        // Assert
        Assert.Contains(TidalQuality.High, qualities);
        Assert.Contains(TidalQuality.Lossless, qualities);
    }

    [Fact]
    public async Task TidalStreamService_GetAvailableQualities_WithUnavailableTrack_ReturnsEmpty()
    {
        // Arrange
        this._mockApiClient.SetupThrowExceptionForAllQualities("unavailable_track");

        // Act
        List<TidalQuality> qualities = await this._streamService.GetAvailableQualitiesForTrackAsync("unavailable_track");

        // Assert
        Assert.Empty(qualities);
    }

    [Fact]
    public async Task TidalStreamService_GetStreamInfoWithManifestParsing_WithValidManifest_ReturnsStreamInfo()
    {
        // Arrange
        string testManifest = CreateTestDashManifest();
        string encodedManifest = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(testManifest));

        // Act
        TidalStreamInfo result = await this._streamService.GetStreamInfoWithManifestParsingAsync(
            "track123", TidalQuality.Lossless, encodedManifest, "application/dash+xml");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("track123", result.TrackId);
        Assert.NotEmpty(result.ChunkUrls);
    }

    [Fact]
    public async Task TidalStreamService_GetStreamInfoWithManifestParsing_WithCorruptManifest_ThrowsException()
    {
        // Arrange
        string invalidManifest = "corrupted_manifest_data";

        // Act & Assert
        _ = await Assert.ThrowsAsync<FormatException>(() =>
            this._streamService.GetStreamInfoWithManifestParsingAsync(
                "track123", TidalQuality.Lossless, invalidManifest, "application/dash+xml"));
    }

    private static string CreateTestDashManifest()
    {
        return @"<?xml version=""1.0""?>
        <MPD>
            <Period>
                <AdaptationSet codecs=""flac"">
                    <SegmentTemplate media=""https://test.com/chunk1.flac"" />
                </AdaptationSet>
            </Period>
        </MPD>";
    }
}

// Enhanced mock for comprehensive testing
public class MockTidalApiClient : ITidalCore
{
    private readonly Dictionary<string, TidalStreamInfo> _streamInfoResponses = [];
    private readonly Dictionary<string, Exception> _exceptionResponses = [];
    private readonly Dictionary<string, List<TidalQuality>> _qualityResponses = [];

    public void SetupStreamInfo(string trackId, TidalStreamInfo streamInfo)
    {
        this._streamInfoResponses[trackId] = streamInfo;
    }

    public void SetupThrowException(string trackId)
    {
        this._exceptionResponses[trackId] = new InvalidOperationException("Stream unavailable");
    }

    public void SetupMultipleQualities(string trackId, params TidalQuality[] qualities)
    {
        foreach (TidalQuality quality in qualities)
        {
            string key = $"{trackId}_{quality}";
            this._streamInfoResponses[key] = new TidalStreamInfo(
                trackId, ["url"], ".flac", "audio/flac", false, null);
        }
    }

    public void SetupThrowExceptionForAllQualities(string trackId)
    {
        foreach (TidalQuality quality in Enum.GetValues<TidalQuality>())
        {
            string key = $"{trackId}_{quality}";
            this._exceptionResponses[key] = new InvalidOperationException("Unavailable");
        }
    }

    public Task<TidalTrackInfo> GetTrackAsync(string trackId, CancellationToken cancellationToken = default)
    {
        TidalTrackInfo track = new(trackId, "Test Track", ["Artist"],
            "album", "Album", 1, 240, TidalQuality.Lossless, true, DateTime.Now);
        return Task.FromResult(track);
    }

    public Task<TidalAlbumInfo> GetAlbumAsync(string albumId, CancellationToken cancellationToken = default)
    {
        TidalAlbumInfo album = new(albumId, "Test Album", ["Artist"],
            [], [TidalQuality.Lossless],
            DateTime.Now, "cover", true);
        return Task.FromResult(album);
    }

    public Task<TidalSearchResults> SearchAsync(string query, int limit = 100, CancellationToken cancellationToken = default)
    {
        TidalSearchResults results = new(
            [], [], [], 0, false);
        return Task.FromResult(results);
    }

    public Task<TidalStreamInfo> GetStreamInfoAsync(string trackId, TidalQuality quality, CancellationToken cancellationToken = default)
    {
        string key = $"{trackId}_{quality}";

        if (this._exceptionResponses.ContainsKey(trackId) || this._exceptionResponses.ContainsKey(key))
            throw this._exceptionResponses.ContainsKey(key) ? this._exceptionResponses[key] : this._exceptionResponses[trackId];

        if (this._streamInfoResponses.ContainsKey(key))
            return Task.FromResult(this._streamInfoResponses[key]);

        if (this._streamInfoResponses.ContainsKey(trackId))
            return Task.FromResult(this._streamInfoResponses[trackId]);

        TidalStreamInfo defaultStream = new(trackId, ["default"], ".flac", "audio/flac", false, null);
        return Task.FromResult(defaultStream);
    }

    public Task<List<TidalTrackInfo>> GetAlbumTracksAsync(string albumId, CancellationToken cancellationToken = default)
    {
        List<TidalTrackInfo> tracks =
        [
            new(albumId + "_t1", "Track 1", ["Artist"], albumId, "Album", 1, 200, TidalQuality.Lossless, true, DateTime.Now)
        ];
        return Task.FromResult(tracks);
    }

    public Task<TidalAlbumInfo> GetAlbumWithTracksAsync(string albumId, CancellationToken cancellationToken = default)
    {
        List<TidalTrackInfo> tracks =
        [
            new(albumId + "_t1", "Track 1", ["Artist"], albumId, "Album", 1, 200, TidalQuality.Lossless, true, DateTime.Now)
        ];
        TidalAlbumInfo album = new(albumId, "Album", ["Artist"], tracks, [TidalQuality.Lossless], DateTime.Now, "cover", true);
        return Task.FromResult(album);
    }

    public Task<bool> IsAuthenticatedAsync()
    {
        return Task.FromResult(true);
    }
}



