using Tidalarr.Core.Models;
using Tidalarr.Infrastructure.Storage;
using Xunit;

namespace Tidalarr.Tests;

public class FileTokenStoreTests : IDisposable
{
    private readonly string _testStoragePath;
    private readonly FileTokenStore _storage;
    
    public FileTokenStoreTests()
    {
        _testStoragePath = Path.Combine(Path.GetTempPath(), $"tidalarr_test_{Guid.NewGuid():N}.json");
        _storage = new FileTokenStore(_testStoragePath);
    }
    
    [Fact]
    public async Task SaveAndLoadTokens_ValidTokens_RoundTripSuccessful()
    {
        // Arrange
        var tokens = new TidalTokens(
            AccessToken: "test_access_token",
            RefreshToken: "test_refresh_token",
            TokenType: "Bearer",
            ExpiresAt: DateTime.UtcNow.AddHours(1),
            SessionId: "session123",
            CountryCode: "US",
            UserId: "12345"
        );
        
        // Act
        await _storage.SaveTokensAsync(tokens);
        var loadedTokens = await _storage.LoadTokensAsync();
        
        // Assert
        Assert.NotNull(loadedTokens);
        Assert.Equal(tokens.AccessToken, loadedTokens.AccessToken);
        Assert.Equal(tokens.RefreshToken, loadedTokens.RefreshToken);
        Assert.Equal(tokens.SessionId, loadedTokens.SessionId);
        Assert.Equal(tokens.CountryCode, loadedTokens.CountryCode);
    }
    
    [Fact]
    public async Task LoadTokens_NoFileExists_ReturnsNull()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid():N}.json");
        var storage = new FileTokenStore(nonExistentPath);
        
        // Act
        var tokens = await storage.LoadTokensAsync();
        
        // Assert
        Assert.Null(tokens);
    }
    
    [Fact]
    public async Task LoadTokens_CorruptedFile_ReturnsNull()
    {
        // Arrange
        await File.WriteAllTextAsync(_testStoragePath, "invalid json content");
        
        // Act
        var tokens = await _storage.LoadTokensAsync();
        
        // Assert
        Assert.Null(tokens); // Should gracefully handle corruption
    }
    
    [Fact]
    public async Task DeleteTokens_FileExists_RemovesFile()
    {
        // Arrange
        var tokens = new TidalTokens("test", "test", "Bearer", DateTime.UtcNow.AddHours(1), "session", "US", "123");
        await _storage.SaveTokensAsync(tokens);
        Assert.True(File.Exists(_testStoragePath));
        
        // Act
        await _storage.DeleteTokensAsync();
        
        // Assert
        Assert.False(File.Exists(_testStoragePath));
    }
    
    [Fact]
    public async Task SaveTokens_InvalidPath_ThrowsException()
    {
        // Arrange
        var invalidPath = "<>|*?invalid:path";
        var storage = new FileTokenStore(invalidPath);
        var tokens = new TidalTokens("test", "test", "Bearer", DateTime.UtcNow.AddHours(1), "session", "US", "123");
        
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            storage.SaveTokensAsync(tokens));
    }
    
    public void Dispose()
    {
        try
        {
            if (File.Exists(_testStoragePath))
                File.Delete(_testStoragePath);
        }
        catch
        {
            // Ignore cleanup errors in tests
        }
    }
}



