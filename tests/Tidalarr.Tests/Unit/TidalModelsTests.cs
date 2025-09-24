using Tidalarr.Core.Models;
using Xunit;

namespace Tidalarr.Tests.Unit;

/// <summary>
/// 100% Coverage: TidalModels record types testing
/// Tests all record constructors, properties, methods, and edge cases
/// </summary>
public class TidalModelsTests
{
    #region TidalTokens Tests
    
    [Fact]
    public void TidalTokens_IsExpired_WhenExpiresAtInPast_ReturnsTrue()
    {
        // Arrange
        var expiredTokens = new TidalTokens(
            "access", "refresh", "Bearer",
            DateTime.UtcNow.AddMinutes(-10), // 10 minutes ago
            "session", "US", "123");
        
        // Act & Assert
        Assert.True(expiredTokens.IsExpired);
    }
    
    [Fact]
    public void TidalTokens_IsExpired_WithFiveMinuteBuffer_ReturnsTrue()
    {
        // Arrange - Token expires in 3 minutes (within 5 minute buffer)
        var almostExpiredTokens = new TidalTokens(
            "access", "refresh", "Bearer",
            DateTime.UtcNow.AddMinutes(3), // 3 minutes from now
            "session", "US", "123");
        
        // Act & Assert
        Assert.True(almostExpiredTokens.IsExpired); // Should be true due to 5-minute buffer
    }
    
    [Fact]
    public void TidalTokens_IsExpired_WithValidToken_ReturnsFalse()
    {
        // Arrange - Token expires in 1 hour
        var validTokens = new TidalTokens(
            "access", "refresh", "Bearer",
            DateTime.UtcNow.AddHours(1),
            "session", "US", "123");
        
        // Act & Assert
        Assert.False(validTokens.IsExpired);
    }
    
    [Fact]
    public void TidalTokens_Constructor_AllProperties_AreSetCorrectly()
    {
        // Arrange
        var accessToken = "test_access";
        var refreshToken = "test_refresh";
        var tokenType = "Bearer";
        var expiresAt = DateTime.UtcNow.AddHours(1);
        var sessionId = "session123";
        var countryCode = "US";
        var userId = "user456";
        
        // Act
        var tokens = new TidalTokens(accessToken, refreshToken, tokenType, expiresAt, sessionId, countryCode, userId);
        
        // Assert
        Assert.Equal(accessToken, tokens.AccessToken);
        Assert.Equal(refreshToken, tokens.RefreshToken);
        Assert.Equal(tokenType, tokens.TokenType);
        Assert.Equal(expiresAt, tokens.ExpiresAt);
        Assert.Equal(sessionId, tokens.SessionId);
        Assert.Equal(countryCode, tokens.CountryCode);
        Assert.Equal(userId, tokens.UserId);
    }
    
    #endregion
    
    #region TidalAuthUrl Tests
    
    [Fact]
    public void TidalAuthUrl_Constructor_SetsAllProperties()
    {
        // Arrange & Act
        var authUrl = new TidalAuthUrl("https://test.url", "verifier123", "state456", string.Empty);
        
        // Assert
        Assert.Equal("https://test.url", authUrl.AuthorizationUrl);
        Assert.Equal("verifier123", authUrl.CodeVerifier);
        Assert.Equal("state456", authUrl.State);
        Assert.Equal(string.Empty, authUrl.ClientUniqueKey);
    }
    
    #endregion
    
    #region TidalCredentials Tests
    
    [Fact]
    public void TidalCredentials_Constructor_SetsRedirectUrl()
    {
        // Arrange & Act
        var credentials = new TidalCredentials("https://tidal.com/callback");
        
        // Assert
        Assert.Equal("https://tidal.com/callback", credentials.RedirectUrl);
    }
    
    #endregion
    
    #region TidalTrackInfo Tests
    
    [Fact]
    public void TidalTrackInfo_Constructor_WithValidData_SetsAllProperties()
    {
        // Arrange
        var artists = new List<string> { "Artist1", "Artist2" };
        var releaseDate = new DateTime(2023, 5, 15);
        
        // Act
        var track = new TidalTrackInfo(
            "track123", "Test Track", artists, "album456", "Test Album",
            3, 240, TidalQuality.Lossless, true, releaseDate);
        
        // Assert
        Assert.Equal("track123", track.Id);
        Assert.Equal("Test Track", track.Title);
        Assert.Equal(artists, track.Artists);
        Assert.Equal("album456", track.AlbumId);
        Assert.Equal("Test Album", track.AlbumTitle);
        Assert.Equal(3, track.TrackNumber);
        Assert.Equal(240, track.Duration);
        Assert.Equal(TidalQuality.Lossless, track.Quality);
        Assert.True(track.IsAvailable);
        Assert.Equal(releaseDate, track.ReleaseDate);
    }
    
    #endregion
    
    #region TidalAlbumInfo Tests
    
    [Fact]
    public void TidalAlbumInfo_Constructor_WithValidData_SetsAllProperties()
    {
        // Arrange
        var artists = new List<string> { "Artist" };
        var tracks = new List<TidalTrackInfo>();
        var qualities = new List<TidalQuality> { TidalQuality.Lossless };
        var releaseDate = new DateTime(2023, 1, 1);
        
        // Act
        var album = new TidalAlbumInfo(
            "album123", "Test Album", artists, tracks, qualities,
            releaseDate, "cover123", true);
        
        // Assert
        Assert.Equal("album123", album.Id);
        Assert.Equal("Test Album", album.Title);
        Assert.Equal(artists, album.Artists);
        Assert.Equal(tracks, album.Tracks);
        Assert.Equal(qualities, album.AvailableQualities);
        Assert.Equal(releaseDate, album.ReleaseDate);
        Assert.Equal("cover123", album.CoverArtId);
        Assert.True(album.IsAvailable);
    }
    
    #endregion
    
    #region TidalSearchResults Tests
    
    [Fact]
    public void TidalSearchResults_Constructor_WithValidData_SetsCorrectly()
    {
        // Arrange
        var albums = new List<TidalAlbumInfo>();
        var tracks = new List<TidalTrackInfo>();
        
        // Act
        var results = new TidalSearchResults(albums, tracks, 10, true);
        
        // Assert
        Assert.Equal(albums, results.Albums);
        Assert.Equal(tracks, results.Tracks);
        Assert.Equal(10, results.TotalCount);
        Assert.True(results.HasMore);
    }
    
    #endregion
    
    #region TidalCallbackResult Tests
    
    [Fact]
    public void TidalCallbackResult_Success_CreatesSuccessResult()
    {
        // Act
        var result = TidalCallbackResult.Success("auth_code", "state123");
        
        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("auth_code", result.AuthCode);
        Assert.Equal("state123", result.State);
        Assert.Empty(result.ErrorMessage);
    }
    
    [Fact]
    public void TidalCallbackResult_Failure_CreatesFailureWithMessage()
    {
        // Act
        var result = TidalCallbackResult.Failure("OAuth error occurred");
        
        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("OAuth error occurred", result.ErrorMessage);
        Assert.Empty(result.AuthCode);
        Assert.Empty(result.State);
    }
    
    #endregion
    
    #region TidalStreamInfo Tests
    
    [Fact]
    public void TidalStreamInfo_Constructor_SetsAllProperties()
    {
        // Arrange
        var chunkUrls = new[] { "url1", "url2" };
        
        // Act
        var streamInfo = new TidalStreamInfo(
            "track123", chunkUrls, ".flac", "audio/flac", true, "token");
        
        // Assert
        Assert.Equal("track123", streamInfo.TrackId);
        Assert.Equal(chunkUrls, streamInfo.ChunkUrls);
        Assert.Equal(".flac", streamInfo.FileExtension);
        Assert.Equal("audio/flac", streamInfo.MimeType);
        Assert.True(streamInfo.IsEncrypted);
        Assert.Equal("token", streamInfo.SecurityToken);
    }
    
    #endregion
    
    #region TidalManifest Tests
    
    [Fact]
    public void TidalManifest_Constructor_SetsAllProperties()
    {
        // Arrange
        var chunkUrls = new[] { "chunk1", "chunk2" };
        
        // Act
        var manifest = new TidalManifest(
            chunkUrls, "flac", "audio/flac", ".flac", 44100, false, null, null);
        
        // Assert
        Assert.Equal(chunkUrls, manifest.ChunkUrls);
        Assert.Equal("flac", manifest.Codec);
        Assert.Equal("audio/flac", manifest.MimeType);
        Assert.Equal(".flac", manifest.FileExtension);
        Assert.Equal(44100, manifest.SampleRate);
        Assert.False(manifest.IsEncrypted);
        Assert.Null(manifest.KeyId);
        Assert.Null(manifest.SecurityToken);
    }
    
    #endregion
    
    #region Enum Coverage Tests
    
    [Theory]
    [InlineData(TidalQuality.Low)]
    [InlineData(TidalQuality.High)]
    [InlineData(TidalQuality.Lossless)]
    [InlineData(TidalQuality.HiRes)]
    public void TidalQuality_AllValues_CanBeUsed(TidalQuality quality)
    {
        // Test all enum values are defined
        Assert.True(Enum.IsDefined(typeof(TidalQuality), quality));
    }
    
    #endregion
    
    #region Record Equality Tests
    
    [Fact]
    public void TidalTokens_EqualityComparison_WithSameValues_AreEqual()
    {
        // Arrange
        var tokens1 = new TidalTokens("access", "refresh", "Bearer", DateTime.UtcNow, "session", "US", "123");
        var tokens2 = new TidalTokens("access", "refresh", "Bearer", tokens1.ExpiresAt, "session", "US", "123");
        
        // Act & Assert
        Assert.Equal(tokens1, tokens2);
        Assert.True(tokens1 == tokens2);
        Assert.False(tokens1 != tokens2);
    }
    
    [Fact]
    public void TidalCallbackResult_EqualityComparison_WithDifferentValues_AreNotEqual()
    {
        // Arrange
        var result1 = TidalCallbackResult.Success("code1", "state1");
        var result2 = TidalCallbackResult.Success("code2", "state1");
        
        // Act & Assert
        Assert.NotEqual(result1, result2);
        Assert.False(result1 == result2);
        Assert.True(result1 != result2);
    }
    
    #endregion
}


