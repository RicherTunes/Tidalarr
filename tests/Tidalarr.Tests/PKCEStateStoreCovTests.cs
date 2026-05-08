using System.Text.Json;
using Tidalarr.Infrastructure.Storage;

namespace Tidalarr.Tests;

public class PKCEStateStoreCovTests
{
    // ========== IsCallbackStateMatch - ArgumentNullException (line 31) ==========
    [Fact]
    public void IsCallbackStateMatch_WhenStoredStateIsNull_ThrowsArgumentNullException()
    {
        // Line 31: throw new ArgumentNullException(nameof(storedState))
        ArgumentNullException ex = Assert.Throws<ArgumentNullException>(
            () => PKCEStateStore.IsCallbackStateMatch(null!, "state"));

        Assert.Equal("storedState", ex.ParamName);
    }

    // ========== Constructor - ArgumentNullException (line 169) ==========
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WhenConfigPathIsInvalid_ThrowsArgumentNullException(string? configPath)
    {
        // Line 169: throw new ArgumentNullException(nameof(configPath), "Config path is required for PKCE state storage")
        ArgumentNullException ex = Assert.Throws<ArgumentNullException>(
            () => new PKCEStateStore(configPath!));

        Assert.Equal("configPath", ex.ParamName);
    }

    // ========== Constructor - Valid path creates directory ==========
    [Fact]
    public void Constructor_WithValidPath_CreatesStorageDirectory()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "tidalarr-cov-tests", Guid.NewGuid().ToString("N"));

        try
        {
            _ = new PKCEStateStore(tempDir);
            Assert.True(Directory.Exists(tempDir));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    // ========== SaveStateAsync - Updates cache and persists ==========
    [Fact]
    public async Task SaveStateAsync_WithValidState_UpdatesCacheAndPersistsToFile()
    {
        // Lines 177-193: SaveStateAsync normal path
        string tempDir = Path.Combine(Path.GetTempPath(), "tidalarr-cov-tests", Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(tempDir);

        try
        {
            PKCEStateStore store = new(tempDir);
            PKCEState state = new("https://test.url/auth", "verifier123", "state456", "key789", DateTime.UtcNow);

            await store.SaveStateAsync(state);

            string filePath = Path.Combine(tempDir, "pkce_state.json");
            Assert.True(File.Exists(filePath));

            // File is encrypted via FileTokenStore — assert envelope shape and that the secret is not on disk.
            string raw = File.ReadAllText(filePath);
            using JsonDocument doc = JsonDocument.Parse(raw);
            Assert.True(doc.RootElement.TryGetProperty("v", out JsonElement v) && v.GetInt32() == 2,
                "Persisted PKCE state must be in v=2 protected envelope format");
            Assert.False(raw.Contains("verifier123", StringComparison.Ordinal),
                "Plaintext code_verifier must not appear in the persisted file");

            // Round-trip via the store to verify we can recover the original.
            PKCEState? loaded = await store.LoadStateAsync();
            Assert.NotNull(loaded);
            Assert.Equal("https://test.url/auth", loaded!.AuthorizationUrl);
            Assert.Equal("verifier123", loaded.CodeVerifier);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    // ========== LoadStateAsync - Cache hit unexpired ==========
    [Fact]
    public async Task LoadStateAsync_WhenCacheHitUnexpired_ReturnsCachedState()
    {
        // Lines 202-208: Cache hit with unexpired state
        string tempDir = Path.Combine(Path.GetTempPath(), "tidalarr-cov-tests", Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(tempDir);

        try
        {
            PKCEStateStore store = new(tempDir);
            PKCEState state = new("https://cached.url/auth", "cachedVerifier", "cachedState", "cachedKey", DateTime.UtcNow);
            await store.SaveStateAsync(state);

            // Load from cache (not disk)
            PKCEState? loaded = await store.LoadStateAsync();

            Assert.NotNull(loaded);
            Assert.Equal("https://cached.url/auth", loaded!.AuthorizationUrl);
            Assert.Equal("cachedVerifier", loaded.CodeVerifier);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    // ========== LoadStateAsync - File not exist ==========
    [Fact]
    public async Task LoadStateAsync_WhenFileNotExist_ReturnsNull()
    {
        // Lines 214-216: File doesn't exist
        string tempDir = Path.Combine(Path.GetTempPath(), "tidalarr-cov-tests", Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(tempDir);

        try
        {
            PKCEStateStore store = new(tempDir);
            PKCEState? result = await store.LoadStateAsync();

            Assert.Null(result);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    // ========== LoadStateAsync - Empty file ==========
    [Fact]
    public async Task LoadStateAsync_WhenFileEmpty_ReturnsNull()
    {
        // Lines 220-222: Empty json file
        string tempDir = Path.Combine(Path.GetTempPath(), "tidalarr-cov-tests", Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(tempDir);

        try
        {
            string filePath = Path.Combine(tempDir, "pkce_state.json");
            File.WriteAllText(filePath, "");

            PKCEStateStore store = new(tempDir);
            PKCEState? result = await store.LoadStateAsync();

            Assert.Null(result);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    // ========== LoadStateAsync - Expired state on disk ==========
    [Fact]
    public async Task LoadStateAsync_WhenStateExpiredOnDisk_DeletesAndReturnsNull()
    {
        // Lines 228-231: Expired state triggers delete
        string tempDir = Path.Combine(Path.GetTempPath(), "tidalarr-cov-tests", Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(tempDir);

        try
        {
            string filePath = Path.Combine(tempDir, "pkce_state.json");
            string expiredJson = /*lang=json,strict*/ $$"""
                {
                  "authorizationUrl": "https://expired.url/auth",
                  "codeVerifier": "expiredVerifier",
                  "state": "expiredState",
                  "clientUniqueKey": "expiredKey",
                  "createdAt": "{{DateTime.UtcNow.AddHours(-1):O}}"
                }
                """;
            File.WriteAllText(filePath, expiredJson);

            PKCEStateStore store = new(tempDir);
            PKCEState? result = await store.LoadStateAsync();

            Assert.Null(result);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    // ========== LoadStateAsync - Valid state from disk ==========
    [Fact]
    public async Task LoadStateAsync_WhenValidStateOnDisk_ReturnsState()
    {
        // Lines 235-240: Load from disk and update cache
        string tempDir = Path.Combine(Path.GetTempPath(), "tidalarr-cov-tests", Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(tempDir);

        try
        {
            string filePath = Path.Combine(tempDir, "pkce_state.json");
            string validJson = /*lang=json,strict*/ $$"""
                {
                  "authorizationUrl": "https://valid.url/auth",
                  "codeVerifier": "validVerifier",
                  "state": "validState",
                  "clientUniqueKey": "validKey",
                  "createdAt": "{{DateTime.UtcNow:O}}"
                }
                """;
            File.WriteAllText(filePath, validJson);

            PKCEStateStore store = new(tempDir);
            PKCEState? result = await store.LoadStateAsync();

            Assert.NotNull(result);
            Assert.Equal("https://valid.url/auth", result!.AuthorizationUrl);
            Assert.Equal("validVerifier", result.CodeVerifier);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    // ========== DeleteStateAsync - Removes from cache and deletes file ==========
    [Fact]
    public async Task DeleteStateAsync_WithExistingState_RemovesFromCacheAndDeletesFile()
    {
        // Lines 248-267: DeleteStateAsync
        string tempDir = Path.Combine(Path.GetTempPath(), "tidalarr-cov-tests", Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(tempDir);

        try
        {
            PKCEStateStore store = new(tempDir);
            PKCEState state = new("https://delete.url/auth", "deleteVerifier", "deleteState", "deleteKey", DateTime.UtcNow);
            await store.SaveStateAsync(state);

            string filePath = Path.Combine(tempDir, "pkce_state.json");
            Assert.True(File.Exists(filePath));

            await store.DeleteStateAsync();

            Assert.False(File.Exists(filePath));
            PKCEState? loaded = await store.LoadStateAsync();
            Assert.Null(loaded);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    // ========== RegenerateCodes - With null/empty path returns early ==========
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RegenerateCodes_WithInvalidPath_ReturnsWithoutError(string? configPath)
    {
        // Lines 152-155: Early return for null/whitespace
        // Should not throw
        PKCEStateStore.RegenerateCodes(configPath);
    }

    // ========== RegenerateCodes - With valid path generates new state ==========
    [Fact]
    public void RegenerateCodes_WithValidPath_GeneratesNewState()
    {
        // Lines 157-162: Create new state and persist
        string tempDir = Path.Combine(Path.GetTempPath(), "tidalarr-cov-tests", Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(tempDir);

        // Pre-populate with old state
        string filePath = Path.Combine(tempDir, "pkce_state.json");
        File.WriteAllText(filePath, /*lang=json,strict*/ $$"""
            {
              "authorizationUrl": "https://old.url/auth",
              "codeVerifier": "oldVerifier",
              "state": "oldState",
              "clientUniqueKey": "oldKey",
              "createdAt": "{{DateTime.UtcNow:O}}"
            }
            """);

        try
        {
            string? oldUrl = PKCEStateStore.TryReadAuthorizationUrl(tempDir);
            Assert.Equal("https://old.url/auth", oldUrl);

            PKCEStateStore.RegenerateCodes(tempDir);

            string? newUrl = PKCEStateStore.TryReadAuthorizationUrl(tempDir);
            Assert.NotNull(newUrl);
            Assert.NotEqual("https://old.url/auth", newUrl);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    // ========== TryReadAuthorizationUrl - Missing authorizationUrl property ==========
    [Fact]
    public void TryReadAuthorizationUrl_WhenNoAuthorizationUrlProperty_ReturnsNull()
    {
        // Lines 58-60: Missing authorizationUrl property
        string tempDir = Path.Combine(Path.GetTempPath(), "tidalarr-cov-tests", Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(tempDir);

        try
        {
            string filePath = Path.Combine(tempDir, "pkce_state.json");
            File.WriteAllText(filePath, /*lang=json,strict*/ """
                {
                  "codeVerifier": "abc",
                  "state": "def",
                  "clientUniqueKey": "ghi",
                  "createdAt": "2025-01-01T00:00:00Z"
                }
                """);

            string? result = PKCEStateStore.TryReadAuthorizationUrl(tempDir);
            Assert.Null(result);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    // ========== TryReadAuthorizationUrl - Whitespace json returns null ==========
    [Fact]
    public void TryReadAuthorizationUrl_WhenWhitespaceJson_ReturnsNull()
    {
        // Lines 52-54: Whitespace json
        string tempDir = Path.Combine(Path.GetTempPath(), "tidalarr-cov-tests", Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(tempDir);

        try
        {
            string filePath = Path.Combine(tempDir, "pkce_state.json");
            File.WriteAllText(filePath, "   ");

            string? result = PKCEStateStore.TryReadAuthorizationUrl(tempDir);
            Assert.Null(result);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
