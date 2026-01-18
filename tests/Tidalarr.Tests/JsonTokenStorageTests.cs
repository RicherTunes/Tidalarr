using Tidalarr.Core.Models;
using Tidalarr.Infrastructure.Storage;

namespace Tidalarr.Tests;

public class FileTokenStoreTests : IDisposable
{
    private readonly string _testStoragePath;
    private readonly FileTokenStore _storage;

    public FileTokenStoreTests()
    {
        this._testStoragePath = Path.Combine(Path.GetTempPath(), $"tidalarr_test_{Guid.NewGuid():N}.json");
        this._storage = new FileTokenStore(this._testStoragePath);
    }

    [Fact]
    public async Task SaveAndLoadTokens_ValidTokens_RoundTripSuccessful()
    {
        // Arrange
        TidalTokens tokens = new(
            AccessToken: "test_access_token",
            RefreshToken: "test_refresh_token",
            TokenType: "Bearer",
            ExpiresAt: DateTime.UtcNow.AddHours(1),
            SessionId: "session123",
            CountryCode: "US",
            UserId: "12345"
        );

        // Act
        await this._storage.SaveTokensAsync(tokens);
        TidalTokens? loadedTokens = await this._storage.LoadTokensAsync();

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
        string nonExistentPath = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid():N}.json");
        FileTokenStore storage = new(nonExistentPath);

        // Act
        TidalTokens? tokens = await storage.LoadTokensAsync();

        // Assert
        Assert.Null(tokens);
    }

    [Fact]
    public async Task LoadTokens_CorruptedFile_ReturnsNull()
    {
        // Arrange
        await File.WriteAllTextAsync(this._testStoragePath, "invalid json content");

        // Act
        TidalTokens? tokens = await this._storage.LoadTokensAsync();

        // Assert
        Assert.Null(tokens); // Should gracefully handle corruption
    }

    [Fact]
    public async Task DeleteTokens_FileExists_RemovesFile()
    {
        // Arrange
        TidalTokens tokens = new("test", "test", "Bearer", DateTime.UtcNow.AddHours(1), "session", "US", "123");
        await this._storage.SaveTokensAsync(tokens);
        Assert.True(File.Exists(this._testStoragePath));

        // Act
        await this._storage.DeleteTokensAsync();

        // Assert
        Assert.False(File.Exists(this._testStoragePath));
    }

    [Fact]
    public async Task SaveTokens_InvalidPath_ThrowsException()
    {
        // Arrange - use path that is invalid on both platforms
        // On Windows: invalid chars like <>|*?:"
        // On Linux: /dev/null/file - can't create files under /dev/null device
        string invalidPath = OperatingSystem.IsWindows()
            ? "<>|*?invalid:path"
            : "/dev/null/impossible_file_" + Guid.NewGuid().ToString("N");

        TidalTokens tokens = new("test", "test", "Bearer", DateTime.UtcNow.AddHours(1), "session", "US", "123");

        // Act & Assert - exception may throw in constructor (EnsureStorageDirectoryExists) or SaveTokensAsync
        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            FileTokenStore storage = new(invalidPath);
            await storage.SaveTokensAsync(tokens);
        });
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(this._testStoragePath))
                File.Delete(this._testStoragePath);
        }
        catch
        {
            // Ignore cleanup errors in tests
        }
    }
}



