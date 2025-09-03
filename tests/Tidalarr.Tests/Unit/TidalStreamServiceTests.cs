using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Streaming;
using Xunit;

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
        _mockApiClient = new MockTidalApiClient();
        _manifestParser = new TidalManifestParser();
        _streamService = new TidalStreamService(_mockApiClient, _manifestParser);
    }
    
    [Fact]
    public async Task TidalStreamService_GetStreamInfo_WithValidTrack_ReturnsStreamInfo()
    {
        // Arrange
        _mockApiClient.SetupStreamInfo("track123", new TidalStreamInfo(
            "track123", new[] { "url1", "url2" }, ".flac", "audio/flac", false, null));
        
        // Act
        var result = await _streamService.GetStreamInfoAsync("track123", TidalQuality.Lossless);
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal("track123", result.TrackId);
        Assert.Equal(2, result.ChunkUrls.Length);
    }
    
    [Fact]
    public async Task TidalStreamService_ValidateStreamAvailability_WithValidStream_ReturnsTrue()
    {
        // Arrange
        _mockApiClient.SetupStreamInfo("valid_track", new TidalStreamInfo(
            "valid_track", new[] { "url1" }, ".flac", "audio/flac", false, null));
        
        // Act
        var result = await _streamService.ValidateStreamAvailabilityAsync("valid_track", TidalQuality.High);
        
        // Assert
        Assert.True(result);
    }
    
    [Fact]
    public async Task TidalStreamService_ValidateStreamAvailability_WithInvalidStream_ReturnsFalse()
    {
        // Arrange
        _mockApiClient.SetupThrowException("invalid_track");
        
        // Act
        var result = await _streamService.ValidateStreamAvailabilityAsync("invalid_track", TidalQuality.High);
        
        // Assert
        Assert.False(result);
    }
    
    [Fact]
    public async Task TidalStreamService_GetAvailableQualities_WithMultipleQualities_ReturnsAll()
    {
        // Arrange
        _mockApiClient.SetupMultipleQualities("track123", 
            TidalQuality.High, TidalQuality.Lossless);
        
        // Act
        var qualities = await _streamService.GetAvailableQualitiesForTrackAsync("track123");
        
        // Assert
        Assert.Contains(TidalQuality.High, qualities);
        Assert.Contains(TidalQuality.Lossless, qualities);
    }
    
    [Fact]
    public async Task TidalStreamService_GetAvailableQualities_WithUnavailableTrack_ReturnsEmpty()
    {
        // Arrange
        _mockApiClient.SetupThrowExceptionForAllQualities("unavailable_track");
        
        // Act
        var qualities = await _streamService.GetAvailableQualitiesForTrackAsync("unavailable_track");
        
        // Assert
        Assert.Empty(qualities);
    }
    
    [Fact]
    public async Task TidalStreamService_GetStreamInfoWithManifestParsing_WithValidManifest_ReturnsStreamInfo()
    {
        // Arrange
        var testManifest = CreateTestDashManifest();
        var encodedManifest = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(testManifest));
        
        // Act
        var result = await _streamService.GetStreamInfoWithManifestParsingAsync(
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
        var invalidManifest = "corrupted_manifest_data";
        
        // Act & Assert
        await Assert.ThrowsAsync<FormatException>(() =>
            _streamService.GetStreamInfoWithManifestParsingAsync(
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
    private readonly Dictionary<string, TidalStreamInfo> _streamInfoResponses = new();
    private readonly Dictionary<string, Exception> _exceptionResponses = new();
    private readonly Dictionary<string, List<TidalQuality>> _qualityResponses = new();
    
    public void SetupStreamInfo(string trackId, TidalStreamInfo streamInfo)
    {
        _streamInfoResponses[trackId] = streamInfo;
    }
    
    public void SetupThrowException(string trackId)
    {
        _exceptionResponses[trackId] = new InvalidOperationException("Stream unavailable");
    }
    
    public void SetupMultipleQualities(string trackId, params TidalQuality[] qualities)
    {
        foreach (var quality in qualities)
        {
            var key = $"{trackId}_{quality}";
            _streamInfoResponses[key] = new TidalStreamInfo(
                trackId, new[] { "url" }, ".flac", "audio/flac", false, null);
        }
    }
    
    public void SetupThrowExceptionForAllQualities(string trackId)
    {
        foreach (var quality in Enum.GetValues<TidalQuality>())
        {
            var key = $"{trackId}_{quality}";
            _exceptionResponses[key] = new InvalidOperationException("Unavailable");
        }
    }
    
    public Task<TidalTrackInfo> GetTrackAsync(string trackId, CancellationToken cancellationToken = default)
    {
        var track = new TidalTrackInfo(trackId, "Test Track", new List<string> { "Artist" },
            "album", "Album", 1, 240, TidalQuality.Lossless, true, DateTime.Now);
        return Task.FromResult(track);
    }
    
    public Task<TidalAlbumInfo> GetAlbumAsync(string albumId, CancellationToken cancellationToken = default)
    {
        var album = new TidalAlbumInfo(albumId, "Test Album", new List<string> { "Artist" },
            new List<TidalTrackInfo>(), new List<TidalQuality> { TidalQuality.Lossless },
            DateTime.Now, "cover", true);
        return Task.FromResult(album);
    }
    
    public Task<TidalSearchResults> SearchAsync(string query, int limit = 100, CancellationToken cancellationToken = default)
    {
        var results = new TidalSearchResults(
            new List<TidalAlbumInfo>(), new List<TidalTrackInfo>(), 0, false);
        return Task.FromResult(results);
    }
    
    public Task<TidalStreamInfo> GetStreamInfoAsync(string trackId, TidalQuality quality, CancellationToken cancellationToken = default)
    {
        var key = $"{trackId}_{quality}";
        
        if (_exceptionResponses.ContainsKey(trackId) || _exceptionResponses.ContainsKey(key))
            throw (_exceptionResponses.ContainsKey(key) ? _exceptionResponses[key] : _exceptionResponses[trackId]);
            
        if (_streamInfoResponses.ContainsKey(key))
            return Task.FromResult(_streamInfoResponses[key]);
            
        if (_streamInfoResponses.ContainsKey(trackId))
            return Task.FromResult(_streamInfoResponses[trackId]);
        
        var defaultStream = new TidalStreamInfo(trackId, new[] { "default" }, ".flac", "audio/flac", false, null);
        return Task.FromResult(defaultStream);
    }

    public Task<List<TidalTrackInfo>> GetAlbumTracksAsync(string albumId, CancellationToken cancellationToken = default)
    {
        var tracks = new List<TidalTrackInfo>
        {
            new(albumId + "_t1", "Track 1", new List<string>{"Artist"}, albumId, "Album", 1, 200, TidalQuality.Lossless, true, DateTime.Now)
        };
        return Task.FromResult(tracks);
    }

    public Task<TidalAlbumInfo> GetAlbumWithTracksAsync(string albumId, CancellationToken cancellationToken = default)
    {
        var tracks = new List<TidalTrackInfo>
        {
            new(albumId + "_t1", "Track 1", new List<string>{"Artist"}, albumId, "Album", 1, 200, TidalQuality.Lossless, true, DateTime.Now)
        };
        var album = new TidalAlbumInfo(albumId, "Album", new List<string>{"Artist"}, tracks, new List<TidalQuality>{TidalQuality.Lossless}, DateTime.Now, "cover", true);
        return Task.FromResult(album);
    }

    public Task<bool> IsAuthenticatedAsync() => Task.FromResult(true);
}
