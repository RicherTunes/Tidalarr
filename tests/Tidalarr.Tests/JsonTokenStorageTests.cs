using System.Text.Json;
using Lidarr.Plugin.Common.Interfaces;
using Lidarr.Plugin.Common.Services.Authentication;
using Tidalarr.Core.Models;
using Tidalarr.Infrastructure.Storage;

namespace Tidalarr.Tests;

/// <summary>
/// Tests for tidalarr's plaintext-to-encrypted token migration. The encrypted
/// <see cref="FileTokenStore{TSession}"/> itself is owned and tested by Lidarr.Plugin.Common
/// (see AtomicFileTokenStoreTests), so we focus here on the migration helper.
/// </summary>
public class LegacyTokenMigrationTests : IDisposable
{
    private readonly string _configPath;
    private readonly string _legacyTokenPath;

    public LegacyTokenMigrationTests()
    {
        this._configPath = Path.Combine(Path.GetTempPath(), $"tidalarr_legacy_{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(this._configPath);
        this._legacyTokenPath = Path.Combine(this._configPath, "tidal_tokens.json");
    }

    [Fact]
    public async Task MigrateIfPresentAsync_NoLegacyFile_ReturnsFalse()
    {
        FileTokenStore<TidalTokens> store = new(Path.Combine(this._configPath, "tidal_tokens.json"));

        bool migrated = await LegacyTokenMigration.MigrateIfPresentAsync(this._configPath, store);

        Assert.False(migrated);
    }

    [Fact]
    public async Task MigrateIfPresentAsync_PlaintextFile_RewritesInPlaceAsEncrypted()
    {
        // Arrange: write a plaintext TidalTokens JSON file in the legacy format.
        TidalTokens legacy = new(
            AccessToken: "legacy_access",
            RefreshToken: "legacy_refresh",
            TokenType: "Bearer",
            ExpiresAt: DateTime.UtcNow.AddHours(1),
            SessionId: "sess",
            CountryCode: "US",
            UserId: "uid");
        JsonSerializerOptions opts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        await File.WriteAllTextAsync(this._legacyTokenPath, JsonSerializer.Serialize(legacy, opts));

        FileTokenStore<TidalTokens> store = new(this._legacyTokenPath);

        // Act
        bool migrated = await LegacyTokenMigration.MigrateIfPresentAsync(this._configPath, store);

        // Assert: migration succeeded; the file now exists in encrypted form at the same path
        // (common's atomic save overwrote the plaintext in-place).
        Assert.True(migrated);
        Assert.True(File.Exists(this._legacyTokenPath));

        // File should now be in protected envelope format (v=2), not the original plaintext layout.
        string protectedContents = await File.ReadAllTextAsync(this._legacyTokenPath);
        Assert.Matches(@"""v""\s*:\s*2", protectedContents);
        Assert.DoesNotContain("legacy_access", protectedContents);
        Assert.DoesNotContain("legacy_refresh", protectedContents);

        // Re-load via the same store instance (avoids cross-protector-instance roundtrip
        // sensitivity in environments where the protector is keyed to process state).
        TokenEnvelope<TidalTokens>? envelope = await store.LoadAsync();
        Assert.NotNull(envelope);
        Assert.Equal("legacy_access", envelope!.Session.AccessToken);
        Assert.Equal("legacy_refresh", envelope.Session.RefreshToken);
        Assert.Equal("sess", envelope.Session.SessionId);
    }

    [Fact]
    public async Task MigrateIfPresentAsync_AlreadyEncryptedFile_NoOps()
    {
        // Arrange: the file already looks like common's protected envelope.
        const string protectedJson = """{"v":2,"alg":"dpapi","payload":"abc"}""";
        await File.WriteAllTextAsync(this._legacyTokenPath, protectedJson);

        FileTokenStore<TidalTokens> store = new(this._legacyTokenPath);

        // Act
        bool migrated = await LegacyTokenMigration.MigrateIfPresentAsync(this._configPath, store);

        // Assert: helper recognises common's format and doesn't overwrite or delete it.
        Assert.False(migrated);
        Assert.True(File.Exists(this._legacyTokenPath));
        Assert.Equal(protectedJson, await File.ReadAllTextAsync(this._legacyTokenPath));
    }

    [Fact]
    public async Task MigrateIfPresentAsync_PersistedEnvelopeFormat_NoOps()
    {
        // Arrange: file uses common's pre-protected persisted-envelope shape.
        const string persistedJson = """{"session":{"accessToken":"x"},"expiresAt":null,"metadata":null}""";
        await File.WriteAllTextAsync(this._legacyTokenPath, persistedJson);

        FileTokenStore<TidalTokens> store = new(this._legacyTokenPath);

        // Act
        bool migrated = await LegacyTokenMigration.MigrateIfPresentAsync(this._configPath, store);

        // Assert
        Assert.False(migrated);
        Assert.True(File.Exists(this._legacyTokenPath));
    }

    [Fact]
    public async Task MigrateIfPresentAsync_CorruptedFile_LeavesInPlace()
    {
        // Arrange
        await File.WriteAllTextAsync(this._legacyTokenPath, "not valid json {");
        FileTokenStore<TidalTokens> store = new(this._legacyTokenPath);

        // Act
        bool migrated = await LegacyTokenMigration.MigrateIfPresentAsync(this._configPath, store);

        // Assert: helper does not delete a file it can't migrate, so an operator can recover manually.
        Assert.False(migrated);
        Assert.True(File.Exists(this._legacyTokenPath));
    }

    [Fact]
    public async Task MigrateIfPresentAsync_NullOrEmptyConfigPath_ReturnsFalse()
    {
        FileTokenStore<TidalTokens> store = new(this._legacyTokenPath);

        Assert.False(await LegacyTokenMigration.MigrateIfPresentAsync(null, store));
        Assert.False(await LegacyTokenMigration.MigrateIfPresentAsync(string.Empty, store));
        Assert.False(await LegacyTokenMigration.MigrateIfPresentAsync("   ", store));
    }

    [Fact]
    public async Task MigrateIfPresentAsync_Idempotent_SecondCallNoOps()
    {
        // After the first call rewrites the file in common's protected format, a second call
        // must recognise that format via IsCommonFormat and skip migration.
        TidalTokens legacy = new("a", "r", "Bearer", DateTime.UtcNow.AddHours(1), "s", "US", "u");
        JsonSerializerOptions opts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        await File.WriteAllTextAsync(this._legacyTokenPath, JsonSerializer.Serialize(legacy, opts));

        FileTokenStore<TidalTokens> store = new(this._legacyTokenPath);

        bool firstRun = await LegacyTokenMigration.MigrateIfPresentAsync(this._configPath, store);
        bool secondRun = await LegacyTokenMigration.MigrateIfPresentAsync(this._configPath, store);

        Assert.True(firstRun);
        Assert.False(secondRun);

        // File is still in encrypted form; second call did not roundtrip the plaintext through
        // the legacy reader (which would fail).
        string contents = await File.ReadAllTextAsync(this._legacyTokenPath);
        Assert.Matches(@"""v""\s*:\s*2", contents);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(this._configPath))
            {
                Directory.Delete(this._configPath, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors in tests
        }
    }
}
