using System.Text.Json;
using Tidalarr.Infrastructure.Storage;

namespace Tidalarr.Tests.Unit;

public class PKCEStateStoreTests
{
    [Fact]
    public void IsCallbackStateMatch_WhenStateMatches_ReturnsTrue()
    {
        PKCEState state = new("url", "verifier", "abc", "key", DateTime.UtcNow);
        Assert.True(PKCEStateStore.IsCallbackStateMatch(state, "abc"));
    }

    [Fact]
    public void IsCallbackStateMatch_WhenStateDiffers_ReturnsFalse()
    {
        PKCEState state = new("url", "verifier", "abc", "key", DateTime.UtcNow);
        Assert.False(PKCEStateStore.IsCallbackStateMatch(state, "def"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void IsCallbackStateMatch_WhenCallbackStateMissing_ReturnsFalse(string callbackState)
    {
        PKCEState state = new("url", "verifier", "abc", "key", DateTime.UtcNow);
        Assert.False(PKCEStateStore.IsCallbackStateMatch(state, callbackState));
    }

    [Fact]
    public void TryReadAuthorizationUrl_WithEmptyConfigPath_ReturnsNull()
    {
        Assert.Null(PKCEStateStore.TryReadAuthorizationUrl(null));
        Assert.Null(PKCEStateStore.TryReadAuthorizationUrl(string.Empty));
        Assert.Null(PKCEStateStore.TryReadAuthorizationUrl("   "));
    }

    [Fact]
    public void TryReadAuthorizationUrl_WithMissingStateFile_ReturnsNull()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "tidalarr-tests", Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(tempDir);

        try
        {
            Assert.Null(PKCEStateStore.TryReadAuthorizationUrl(tempDir));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void TryReadAuthorizationUrl_WithValidStateFile_ReturnsUrl()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "tidalarr-tests", Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(tempDir);

        try
        {
            string path = Path.Combine(tempDir, "pkce_state.json");
            File.WriteAllText(path, /*lang=json,strict*/ """
                {
                  "authorizationUrl": "https://login.tidal.com/authorize?response_type=code",
                  "codeVerifier": "abc",
                  "state": "def",
                  "clientUniqueKey": "ghi",
                  "createdAt": "2025-01-01T00:00:00Z"
                }
                """);

            Assert.Equal(
                "https://login.tidal.com/authorize?response_type=code",
                PKCEStateStore.TryReadAuthorizationUrl(tempDir));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void TryReadAuthorizationUrl_WithInvalidJson_ReturnsNull()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "tidalarr-tests", Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(tempDir);

        try
        {
            string path = Path.Combine(tempDir, "pkce_state.json");
            File.WriteAllText(path, "{not json");

            Assert.Null(PKCEStateStore.TryReadAuthorizationUrl(tempDir));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void TryGetOrCreateAuthorizationUrl_WithEmptyConfigPath_ReturnsNull()
    {
        Assert.Null(PKCEStateStore.TryGetOrCreateAuthorizationUrl(null));
        Assert.Null(PKCEStateStore.TryGetOrCreateAuthorizationUrl(string.Empty));
        Assert.Null(PKCEStateStore.TryGetOrCreateAuthorizationUrl("   "));
    }

    [Fact]
    public void TryGetOrCreateAuthorizationUrl_WithMissingStateFile_CreatesStateAndReturnsUrl()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "tidalarr-tests", Guid.NewGuid().ToString("N"));

        try
        {
            string? url = PKCEStateStore.TryGetOrCreateAuthorizationUrl(tempDir);
            Assert.False(string.IsNullOrWhiteSpace(url));

            string path = Path.Combine(tempDir, "pkce_state.json");
            Assert.True(File.Exists(path));

            // File is now encrypted (v=2 envelope) — read URL via the public API instead of raw JSON.
            string? readBack = PKCEStateStore.TryReadAuthorizationUrl(tempDir);
            Assert.Equal(url, readBack);

            // Verify the file is NOT plaintext (codeVerifier should not appear on disk).
            string raw = File.ReadAllText(path);
            using JsonDocument document = JsonDocument.Parse(raw);
            Assert.True(document.RootElement.TryGetProperty("v", out JsonElement v) && v.GetInt32() == 2,
                "Persisted PKCE state must be in v=2 protected envelope format");
            Assert.False(raw.Contains("codeVerifier", StringComparison.Ordinal),
                "Plaintext codeVerifier must not appear in the persisted file");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void TryGetOrCreateAuthorizationUrl_WithExistingUnexpiredState_ReusesAuthorizationUrl()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "tidalarr-tests", Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(tempDir);

        try
        {
            string path = Path.Combine(tempDir, "pkce_state.json");
            File.WriteAllText(path, /*lang=json,strict*/ $$"""
                {
                  "authorizationUrl": "https://login.tidal.com/authorize?response_type=code&state=existing",
                  "codeVerifier": "abc",
                  "state": "existing",
                  "clientUniqueKey": "ghi",
                  "createdAt": "{{DateTime.UtcNow:O}}"
                }
                """);

            string? url1 = PKCEStateStore.TryGetOrCreateAuthorizationUrl(tempDir);
            string? url2 = PKCEStateStore.TryGetOrCreateAuthorizationUrl(tempDir);

            Assert.Equal("https://login.tidal.com/authorize?response_type=code&state=existing", url1);
            Assert.Equal(url1, url2);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void TryGetOrCreateAuthorizationUrl_WithExpiredState_ReplacesAuthorizationUrl()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "tidalarr-tests", Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(tempDir);

        try
        {
            string path = Path.Combine(tempDir, "pkce_state.json");
            const string oldUrl = "https://login.tidal.com/authorize?response_type=code&state=expired";
            File.WriteAllText(path, /*lang=json,strict*/ $$"""
                {
                  "authorizationUrl": "{{oldUrl}}",
                  "codeVerifier": "abc",
                  "state": "expired",
                  "clientUniqueKey": "ghi",
                  "createdAt": "{{DateTime.UtcNow.AddHours(-1):O}}"
                }
                """);

            string? newUrl = PKCEStateStore.TryGetOrCreateAuthorizationUrl(tempDir);
            Assert.False(string.IsNullOrWhiteSpace(newUrl));
            Assert.NotEqual(oldUrl, newUrl);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task LegacyPlaintextFile_IsMigratedToEncryptedFormatOnRead()
    {
        // Wave 16 security fix: existing plaintext pkce_state.json files (pre-encryption)
        // must continue to load AND be auto-migrated to the encrypted v=2 envelope.
        string tempDir = Path.Combine(Path.GetTempPath(), "tidalarr-tests", Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(tempDir);

        try
        {
            string path = Path.Combine(tempDir, "pkce_state.json");
            const string secretVerifier = "legacy-secret-verifier-must-be-encrypted";
            File.WriteAllText(path, /*lang=json,strict*/ $$"""
                {
                  "authorizationUrl": "https://login.tidal.com/authorize?response_type=code&state=legacy",
                  "codeVerifier": "{{secretVerifier}}",
                  "state": "legacy",
                  "clientUniqueKey": "ghi",
                  "createdAt": "{{DateTime.UtcNow:O}}"
                }
                """);

            // Reading via the store should both succeed AND trigger migration to encrypted format.
            PKCEStateStore store = new(tempDir);
            PKCEState? loaded = await store.LoadStateAsync();
            Assert.NotNull(loaded);
            Assert.Equal(secretVerifier, loaded!.CodeVerifier);

            // After load, the file should now be the v=2 protected envelope, NOT plaintext.
            string raw = File.ReadAllText(path);
            using JsonDocument doc = JsonDocument.Parse(raw);
            Assert.True(doc.RootElement.TryGetProperty("v", out JsonElement v) && v.GetInt32() == 2,
                "Legacy plaintext file must be migrated to v=2 protected envelope on first read");
            Assert.False(raw.Contains(secretVerifier, StringComparison.Ordinal),
                "After migration the codeVerifier must not appear in the persisted file");

            // The OAuthAuthUrl-derived getter must still work after migration.
            Assert.Equal(loaded.AuthorizationUrl, PKCEStateStore.TryReadAuthorizationUrl(tempDir));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void TryGetOrCreateAuthorizationUrl_WithInvalidJson_OverwritesAndReturnsUrl()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "tidalarr-tests", Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(tempDir);

        try
        {
            string path = Path.Combine(tempDir, "pkce_state.json");
            File.WriteAllText(path, "{not json");

            string? url = PKCEStateStore.TryGetOrCreateAuthorizationUrl(tempDir);
            Assert.False(string.IsNullOrWhiteSpace(url));

            // Verify the URL is readable via the public API after auto-replacing the bad file.
            string? readBack = PKCEStateStore.TryReadAuthorizationUrl(tempDir);
            Assert.Equal(url, readBack);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
