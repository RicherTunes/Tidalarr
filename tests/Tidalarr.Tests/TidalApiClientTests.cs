using System.Net;
using System.Text;
using System.Text.Json;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Api;
using Tidalarr.Infrastructure.Storage;
using Xunit;

namespace Tidalarr.Tests;

public class TidalApiClientTests
{
    [Fact]
    public async Task SearchAsync_ValidQuery_ReturnsResults()
    {
        // Arrange
        var mockSearchResponse = new TidalSearchResponseDto(
            albums: new TidalAlbumsResponseDto(new List<TidalAlbumDto> {
                new("123", "Test Album", new TidalArtistDto("Test Artist", "456"), 
                    DateTime.UtcNow.ToString("yyyy-MM-dd"), 10, 3000, true, "cover123")
            }),
            tracks: new TidalTracksResponseDto(new List<TidalTrackDto>())
        );
        
        var httpClient = CreateMockHttpClient(JsonSerializer.Serialize(mockSearchResponse));
        var mockAuth = CreateAuthenticatedMockAuth();
        var apiClient = new TidalApiClient(httpClient, mockAuth);
        
        // Act
        var results = await apiClient.SearchAsync("test query");
        
        // Assert
        Assert.NotNull(results);
        Assert.Single(results.Albums);
        Assert.Equal("Test Album", results.Albums[0].Title);
        Assert.Equal("Test Artist", results.Albums[0].Artists[0]);
    }
    
    [Fact]
    public async Task GetTrackAsync_ValidId_ReturnsTrack()
    {
        // Arrange
        var mockTrackResponse = new TidalTrackDto(
            id: "123",
            title: "Test Track",
            artist: new TidalArtistDto("Test Artist", "789"),
            album: new TidalAlbumDto("456", "Test Album", new TidalArtistDto("Test Artist", "789"),
                DateTime.UtcNow.ToString("yyyy-MM-dd"), 10, 3000, true, "cover456"),
            trackNumber: 1,
            duration: 240,
            streamReady: true,
            audioQuality: "LOSSLESS"
        );
        
        var httpClient = CreateMockHttpClient(JsonSerializer.Serialize(mockTrackResponse));
        var mockAuth = CreateAuthenticatedMockAuth();
        var apiClient = new TidalApiClient(httpClient, mockAuth);
        
        // Act
        var track = await apiClient.GetTrackAsync("123");
        
        // Assert
        Assert.NotNull(track);
        Assert.Equal("123", track.Id);
        Assert.Equal("Test Track", track.Title);
        Assert.Equal("Test Artist", track.Artists[0]);
        Assert.Equal("456", track.AlbumId);
        Assert.Equal(TidalQuality.Lossless, track.Quality);
    }
    
    [Fact]
    public async Task GetStreamInfoAsync_ValidTrack_ReturnsStreamInfo()
    {
        // Arrange
        var mockStreamResponse = new TidalPlaybackInfoDto(
            manifest: Convert.ToBase64String(Encoding.UTF8.GetBytes(CreateTestDashManifest())),
            manifestMimeType: "application/dash+xml",
            encryptionType: "NONE",
            securityToken: null
        );
        
        var httpClient = CreateMockHttpClient(JsonSerializer.Serialize(mockStreamResponse));
        var mockAuth = CreateAuthenticatedMockAuth();
        var apiClient = new TidalApiClient(httpClient, mockAuth);
        
        // Act
        var streamInfo = await apiClient.GetStreamInfoAsync("123", TidalQuality.Lossless);
        
        // Assert
        Assert.NotNull(streamInfo);
        Assert.Equal("123", streamInfo.TrackId);
        // API client does not parse manifest into chunk URLs; service does that
        Assert.NotNull(streamInfo.MimeType);
    }
    
    [Fact]
    public async Task ApiCall_NotAuthenticated_ThrowsException()
    {
        // Arrange
        var httpClient = new HttpClient();
        var mockAuth = CreateNotAuthenticatedMockAuth();
        var apiClient = new TidalApiClient(httpClient, mockAuth);
        
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            apiClient.SearchAsync("test"));
    }
    
    private static HttpClient CreateMockHttpClient(string jsonResponse, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var mockHandler = new MockHttpMessageHandler(jsonResponse, statusCode);
        return new HttpClient(mockHandler);
    }
    
    private static ITidalAuth CreateAuthenticatedMockAuth()
    {
        var mockAuth = new MockTidalAuth();
        mockAuth.SetAuthenticated(true);
        return mockAuth;
    }
    
    private static ITidalAuth CreateNotAuthenticatedMockAuth()
    {
        var mockAuth = new MockTidalAuth();
        mockAuth.SetAuthenticated(false);
        return mockAuth;
    }
    
    private static string CreateTestDashManifest()
    {
        return @"<?xml version=""1.0"" encoding=""UTF-8""?>
        <MPD>
            <Period>
                <AdaptationSet>
                    <SegmentTemplate media=""https://test.tidal.com/chunk1.flac"" />
                    <SegmentTemplate media=""https://test.tidal.com/chunk2.flac"" />
                </AdaptationSet>
            </Period>
        </MPD>";
    }
}

public class MockTidalAuth : ITidalAuth
{
    private bool _isAuthenticated;
    private TidalTokens? _tokens;
    
    public bool IsAuthenticated => _isAuthenticated;
    
    public void SetAuthenticated(bool authenticated)
    {
        _isAuthenticated = authenticated;
        if (authenticated)
        {
            _tokens = new TidalTokens("test_token", "refresh_token", "Bearer", 
                DateTime.UtcNow.AddHours(1), "session123", "US", "12345");
        }
    }
    
    public Task<TidalAuthUrl> GenerateAuthUrlAsync() => 
        Task.FromResult(new TidalAuthUrl("https://test.url", "verifier", "state", string.Empty));
        
    public Task<TidalTokens> ExchangeCodeAsync(string authCode, string codeVerifier) => 
        Task.FromResult(_tokens!);
        
    public Task<TidalTokens> RefreshTokensAsync(string refreshToken) => 
        Task.FromResult(_tokens!);
        
    public Task<TidalTokens> GetValidTokensAsync()
    {
        if (!_isAuthenticated || _tokens == null)
            throw new InvalidOperationException("Not authenticated");
        return Task.FromResult(_tokens);
    }
}



