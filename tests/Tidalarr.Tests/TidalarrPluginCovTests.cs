using System.Text.Json;
using Lidarr.Plugin.Abstractions.Contracts;
using Lidarr.Plugin.Abstractions.Results;
using Microsoft.Extensions.Logging;
using Moq;
using Tidalarr.Core.Models;
using Tidalarr.Integration;

namespace Tidalarr.Tests;

/// <summary>
/// Coverage tests for TidalarrPlugin.
/// Source: src/Tidalarr/Integration/TidalarrPlugin.cs
/// </summary>
public class TidalarrPluginCovTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_InitializesSettingsProvider()
    {
        // Arrange & Act
        var plugin = new TidalarrPlugin();

        // Assert - Source line 66: SettingsProvider property is initialized
        Assert.NotNull(plugin.SettingsProvider);
        Assert.IsType<TidalarrPlugin>(plugin);
    }

    #endregion

    #region Manifest Property Tests

    [Fact]
    public void Manifest_WhenPluginJsonMissing_ReturnsFallbackManifest()
    {
        // Arrange - Temporarily move plugin.json out of AppContext.BaseDirectory
        // so the Manifest getter exercises its FileNotFoundException fallback branch.
        string baseDir = AppContext.BaseDirectory;
        string manifestPath = Path.Combine(baseDir, "plugin.json");
        string? backupPath = null;
        if (File.Exists(manifestPath))
        {
            backupPath = manifestPath + ".bak-" + Guid.NewGuid().ToString("N");
            File.Move(manifestPath, backupPath);
        }

        try
        {
            var plugin = new TidalarrPlugin();

            // Act - Source lines 26-59: Manifest getter with try-catch fallback
            var manifest = plugin.Manifest;

            // Assert - Fallback values from lines 38-45
            Assert.Equal("tidalarr", manifest.Id);
            Assert.Equal("Tidalarr", manifest.Name);
            Assert.Equal("1.0.1", manifest.Version);
            Assert.Equal("1.x", manifest.ApiVersion);
            Assert.Contains("ConfigPath", manifest.RequiredSettings);
            Assert.Contains("RedirectUrl", manifest.RequiredSettings);
            Assert.Contains("DownloadPath", manifest.RequiredSettings);
        }
        finally
        {
            if (backupPath is not null && File.Exists(backupPath))
            {
                File.Move(backupPath, manifestPath, overwrite: true);
            }
        }
    }

    #endregion

    #region InitializeAsync Tests

    [Fact]
    public async ValueTask InitializeAsync_WithValidContext_InitializesSuccessfully()
    {
        // Arrange
        var plugin = new TidalarrPlugin();
        var context = CreateMockContext();

        // Act - Source lines 69-75: InitializeAsync
        await plugin.InitializeAsync(context);

        // Assert - No exception means success
        // Service provider is built internally via RebuildServiceProvider (line 73)
        await plugin.DisposeAsync();
    }

    [Fact]
    public async ValueTask InitializeAsync_WithNullContext_ThrowsArgumentNullException()
    {
        // Arrange
        var plugin = new TidalarrPlugin();

        // Act & Assert - Source line 71: throw new ArgumentNullException(nameof(context))
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await plugin.InitializeAsync(null!));

        Assert.Equal("context", exception.ParamName);
    }

    #endregion

    #region Services Property Tests (via CreateIndexerAsync/CreateDownloadClientAsync)

    [Fact]
    public async ValueTask CreateIndexerAsync_BeforeInitialization_ThrowsInvalidOperationException()
    {
        // Arrange - Create plugin but don't initialize (no service provider)
        var plugin = new TidalarrPlugin();

        // Act & Assert - Source line 23: throw new InvalidOperationException("Plugin services not initialized.")
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await plugin.CreateIndexerAsync());

        Assert.Equal("Plugin services not initialized.", exception.Message);
    }

    [Fact]
    public async ValueTask CreateDownloadClientAsync_BeforeInitialization_ThrowsInvalidOperationException()
    {
        // Arrange - Create plugin but don't initialize (no service provider)
        var plugin = new TidalarrPlugin();

        // Act & Assert - Source line 23: throw new InvalidOperationException("Plugin services not initialized.")
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await plugin.CreateDownloadClientAsync());

        Assert.Equal("Plugin services not initialized.", exception.Message);
    }

    [Fact]
    public async ValueTask CreateIndexerAsync_AfterInitialization_ReturnsAdapter()
    {
        // Arrange
        var plugin = new TidalarrPlugin();
        var context = CreateMockContext();
        await plugin.InitializeAsync(context);

        // Act - Source lines 131-136: CreateIndexerAsync
        var indexer = await plugin.CreateIndexerAsync();

        // Assert
        Assert.NotNull(indexer);
        await plugin.DisposeAsync();
    }

    [Fact]
    public async ValueTask CreateDownloadClientAsync_AfterInitialization_ReturnsAdapter()
    {
        // Arrange
        var plugin = new TidalarrPlugin();
        var context = CreateMockContext();
        await plugin.InitializeAsync(context);

        // Act - Source lines 138-143: CreateDownloadClientAsync
        var downloadClient = await plugin.CreateDownloadClientAsync();

        // Assert
        Assert.NotNull(downloadClient);
        await plugin.DisposeAsync();
    }

    #endregion

    #region ValidateSettingsWithDiagnostics Tests

    [Fact]
    public void ValidateSettingsWithDiagnostics_WithValidSettings_ReturnsSuccess()
    {
        // Arrange
        var plugin = new TidalarrPlugin();
        var settings = CreateValidSettings();

        // Act - Source lines 78-101: ValidateSettingsWithDiagnostics
        var result = plugin.ValidateSettingsWithDiagnostics(settings);

        // Assert - Lines 86-92: Success path
        Assert.True(result.IsSuccess);
        Assert.Equal("CFG000", result.GetValueOrThrow()["id"]);
        Assert.Equal("Tidal", result.GetValueOrThrow()["service"]);
    }

    [Fact]
    public void ValidateSettingsWithDiagnostics_WithMissingConfigPath_ReturnsFailure()
    {
        // Arrange
        var plugin = new TidalarrPlugin();
        var settings = new Dictionary<string, object?>
        {
            ["ConfigPath"] = "", // Explicit empty to override the non-empty TidalarrSettings default
            ["RedirectUrl"] = "https://tidal.com/callback",
            ["DownloadPath"] = "/downloads"
        };

        // Act - Source lines 78-101: ValidateSettingsWithDiagnostics failure path
        var result = plugin.ValidateSettingsWithDiagnostics(settings);

        // Assert - Lines 94-100: Failure path with CFG100 code
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(PluginErrorCode.ValidationFailed, result.Error.Code);
        Assert.Equal("CFG100", result.Error.Metadata["id"]);
    }

    [Fact]
    public void ValidateSettingsWithDiagnostics_WithInvalidRedirectUrl_ReturnsFailure()
    {
        // Arrange
        var plugin = new TidalarrPlugin();
        var settings = new Dictionary<string, object?>
        {
            ["ConfigPath"] = "/config",
            ["RedirectUrl"] = "not-a-valid-url",
            ["DownloadPath"] = "/downloads"
        };

        // Act
        var result = plugin.ValidateSettingsWithDiagnostics(settings);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(PluginErrorCode.ValidationFailed, result.Error!.Code);
    }

    #endregion

    #region ApplySettingsWithDiagnostics Tests

    [Fact]
    public void ApplySettingsWithDiagnostics_WithValidSettings_ReturnsSuccess()
    {
        // Arrange
        var plugin = new TidalarrPlugin();
        var settings = CreateValidSettings();

        // Act - Source lines 103-129: ApplySettingsWithDiagnostics
        var result = plugin.ApplySettingsWithDiagnostics(settings);

        // Assert - Lines 124-128: Success path
        Assert.True(result.IsSuccess);
        Assert.Equal("CFG000", result.GetValueOrThrow()["id"]);
        Assert.Equal("Tidal", result.GetValueOrThrow()["service"]);
    }

    [Fact]
    public void ApplySettingsWithDiagnostics_WithInvalidSettings_ReturnsFailure()
    {
        // Arrange
        var plugin = new TidalarrPlugin();
        var settings = new Dictionary<string, object?>
        {
            ["ConfigPath"] = "", // Empty - invalid
            ["RedirectUrl"] = "https://tidal.com/callback",
            ["DownloadPath"] = "/downloads"
        };

        // Act - Source lines 103-129: ApplySettingsWithDiagnostics failure path
        var result = plugin.ApplySettingsWithDiagnostics(settings);

        // Assert - Lines 111-119: Failure path
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(PluginErrorCode.ValidationFailed, result.Error.Code);
        Assert.Equal("CFG100", result.Error.Metadata["id"]);
    }

    #endregion

    #region SettingsProvider.Describe Tests

    [Fact]
    public void SettingsProvider_Describe_ReturnsAllDefinitions()
    {
        // Arrange
        var plugin = new TidalarrPlugin();

        // Act - Source lines 299-349: Describe method
        var definitions = plugin.SettingsProvider.Describe();

        // Assert - Count the definitions (14 total; T-3 removed IncludeMqa + ReEncodeAAC as dead settings)
        Assert.Equal(14, definitions.Count);

        // Verify required settings are present
        var keys = definitions.Select(d => d.Key).ToList();
        Assert.Contains(nameof(TidalarrSettings.ConfigPath), keys);
        Assert.Contains(nameof(TidalarrSettings.RedirectUrl), keys);
        Assert.Contains(nameof(TidalarrSettings.DownloadPath), keys);
        Assert.Contains(nameof(TidalarrSettings.PreferredQuality), keys);
    }

    [Fact]
    public void SettingsProvider_Describe_ConfigPathDefinition_IsRequired()
    {
        // Arrange
        var plugin = new TidalarrPlugin();

        // Act
        var definitions = plugin.SettingsProvider.Describe();

        // Assert - Source lines 304-310: ConfigPath definition
        var configPathDef = definitions.First(d => d.Key == nameof(TidalarrSettings.ConfigPath));
        Assert.Equal("Config Path", configPathDef.DisplayName);
        Assert.Equal(SettingDataType.String, configPathDef.DataType);
        Assert.True(configPathDef.IsRequired);
    }

    [Fact]
    public void SettingsProvider_Describe_PreferredQualityDefinition_HasAllowedValues()
    {
        // Arrange
        var plugin = new TidalarrPlugin();

        // Act
        var definitions = plugin.SettingsProvider.Describe();

        // Assert - Source lines 328-335: PreferredQuality definition with AllowedValues
        var qualityDef = definitions.First(d => d.Key == nameof(TidalarrSettings.PreferredQuality));
        Assert.Equal(SettingDataType.Enum, qualityDef.DataType);
        Assert.Contains("Low", qualityDef.AllowedValues);
        Assert.Contains("High", qualityDef.AllowedValues);
        Assert.Contains("Lossless", qualityDef.AllowedValues);
        Assert.Contains("HiRes", qualityDef.AllowedValues);
        Assert.Equal("Lossless", qualityDef.DefaultValue);
    }

    #endregion

    #region SettingsProvider.GetDefaults Tests

    [Fact]
    public void SettingsProvider_GetDefaults_ReturnsAllDefaults()
    {
        // Arrange
        var plugin = new TidalarrPlugin();

        // Act - Source lines 351-372: GetDefaults method
        var defaults = plugin.SettingsProvider.GetDefaults();

        // Assert - 14 default values; T-3 removed IncludeMqa + ReEncodeAAC as dead settings
        Assert.Equal(14, defaults.Count);
    }

    [Fact]
    public void SettingsProvider_GetDefaults_PreferredQuality_IsLossless()
    {
        // Arrange
        var plugin = new TidalarrPlugin();

        // Act
        var defaults = plugin.SettingsProvider.GetDefaults();

        // Assert - Source line 358: PreferredQuality defaults to "Lossless"
        Assert.Equal("Lossless", defaults[nameof(TidalarrSettings.PreferredQuality)]);
    }

    [Fact]
    public void SettingsProvider_GetDefaults_EnableCache_IsTrue()
    {
        // Arrange
        var plugin = new TidalarrPlugin();

        // Act
        var defaults = plugin.SettingsProvider.GetDefaults();

        // Assert - Source line 361: EnableCache defaults to true
        Assert.True((bool)defaults[nameof(TidalarrSettings.EnableCache)]!);
    }

    [Fact]
    public void SettingsProvider_GetDefaults_MaxConcurrentTrackDownloads_Is2()
    {
        // Arrange
        var plugin = new TidalarrPlugin();

        // Act
        var defaults = plugin.SettingsProvider.GetDefaults();

        // Assert - Source line 369: MaxConcurrentTrackDownloads defaults to 2
        Assert.Equal(2, defaults[nameof(TidalarrSettings.MaxConcurrentTrackDownloads)]);
    }

    #endregion

    #region SettingsProvider.Validate Tests

    [Fact]
    public void SettingsProvider_Validate_WithValidSettings_ReturnsSuccess()
    {
        // Arrange
        var plugin = new TidalarrPlugin();
        var settings = CreateValidSettings();

        // Act - Source lines 374-381: Validate method
        var result = plugin.SettingsProvider.Validate(settings);

        // Assert - Line 378: Success path
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void SettingsProvider_Validate_WithInvalidSettings_ReturnsFailure()
    {
        // Arrange
        var plugin = new TidalarrPlugin();
        var settings = new Dictionary<string, object?>
        {
            ["ConfigPath"] = "", // Invalid - empty
            ["RedirectUrl"] = "invalid-url", // Invalid - not a URL
            ["DownloadPath"] = "" // Invalid - empty
        };

        // Act - Source lines 374-381: Validate method failure path
        var result = plugin.SettingsProvider.Validate(settings);

        // Assert - Lines 379-380: Failure path
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void SettingsProvider_Validate_WithUnsupportedMarket_ReturnsFailure()
    {
        // Arrange
        var plugin = new TidalarrPlugin();
        var settings = CreateValidSettings();
        settings["TidalMarket"] = "XX"; // Unsupported market (see line 95: supported markets)

        // Act
        var result = plugin.SettingsProvider.Validate(settings);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Unsupported market"));
    }

    #endregion

    #region SettingsProvider.Apply Tests

    [Fact]
    public void SettingsProvider_Apply_WithValidSettings_ReturnsSuccess()
    {
        // Arrange
        var plugin = new TidalarrPlugin();
        var settings = CreateValidSettings();

        // Act - Source lines 383-397: Apply method
        var result = plugin.SettingsProvider.Apply(settings);

        // Assert - Lines 395-396: Success path
        Assert.True(result.IsValid);
    }

    [Fact]
    public void SettingsProvider_Apply_WithInvalidSettings_ReturnsFailure()
    {
        // Arrange
        var plugin = new TidalarrPlugin();
        var settings = new Dictionary<string, object?>
        {
            ["ConfigPath"] = "",
            ["RedirectUrl"] = "not-a-url",
            ["DownloadPath"] = ""
        };

        // Act - Source lines 383-397: Apply method failure path
        var result = plugin.SettingsProvider.Apply(settings);

        // Assert - Lines 388-391: Failure path
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
    }

    #endregion

    #region DisposeAsync Tests

    [Fact]
    public async ValueTask DisposeAsync_WithoutInitialization_CompletesWithoutError()
    {
        // Arrange
        var plugin = new TidalarrPlugin();

        // Act - Source lines 158-169: DisposeAsync (null service provider path)
        await plugin.DisposeAsync();

        // Assert - No exception means success (line 166: _serviceProvider?.Dispose())
    }

    [Fact]
    public async ValueTask DisposeAsync_AfterInitialization_CompletesWithoutError()
    {
        // Arrange
        var plugin = new TidalarrPlugin();
        var context = CreateMockContext();
        await plugin.InitializeAsync(context);

        // Act - Source lines 158-169: DisposeAsync with IAsyncDisposable path
        await plugin.DisposeAsync();

        // Assert - No exception means success
    }

    [Fact]
    public async ValueTask DisposeAsync_CanBeCalledMultipleTimes()
    {
        // Arrange
        var plugin = new TidalarrPlugin();
        var context = CreateMockContext();
        await plugin.InitializeAsync(context);

        // Act - Call dispose twice
        await plugin.DisposeAsync();
        await plugin.DisposeAsync();

        // Assert - No exception on second dispose
    }

    #endregion

    #region MapToSettings Edge Cases (via ApplySettingsWithDiagnostics)

    [Fact]
    public void ApplySettingsWithDiagnostics_WithJsonElementValues_MapsCorrectly()
    {
        // Arrange
        var plugin = new TidalarrPlugin();
        var settings = new Dictionary<string, object?>
        {
            ["ConfigPath"] = JsonDocument.Parse(@"""/config""").RootElement,
            ["RedirectUrl"] = JsonDocument.Parse(@"""https://tidal.com/callback""").RootElement,
            ["DownloadPath"] = JsonDocument.Parse(@"""/downloads""").RootElement,
            ["PreferredQuality"] = JsonDocument.Parse(@"""HiRes""").RootElement,
            ["EnableCache"] = JsonDocument.Parse(@"false").RootElement,
            ["CacheDuration"] = JsonDocument.Parse(@"30").RootElement
        };

        // Act - Source lines 171-218: MapToSettings with JsonElement handling
        var result = plugin.ApplySettingsWithDiagnostics(settings);

        // Assert - JsonElement values are mapped correctly
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void ApplySettingsWithDiagnostics_WithNullValues_UsesDefaults()
    {
        // Arrange
        var plugin = new TidalarrPlugin();
        var settings = new Dictionary<string, object?>
        {
            ["ConfigPath"] = "/config",
            ["RedirectUrl"] = "https://tidal.com/callback",
            ["DownloadPath"] = "/downloads",
            ["TidalMarket"] = null,
            ["EnableCache"] = null
        };

        // Act - Source lines 221-264: GetStringValue/GetBoolValue with null handling
        var result = plugin.ApplySettingsWithDiagnostics(settings);

        // Assert - Null values are handled gracefully
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void ApplySettingsWithDiagnostics_WithIntAsString_MapsCorrectly()
    {
        // Arrange
        var plugin = new TidalarrPlugin();
        var settings = new Dictionary<string, object?>
        {
            ["ConfigPath"] = "/config",
            ["RedirectUrl"] = "https://tidal.com/callback",
            ["DownloadPath"] = "/downloads",
            ["CacheDuration"] = "30", // Int as string - line 245: string s when int.TryParse
            ["MaxConcurrentTrackDownloads"] = "3"
        };

        // Act
        var result = plugin.ApplySettingsWithDiagnostics(settings);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void ApplySettingsWithDiagnostics_WithBoolAsString_MapsCorrectly()
    {
        // Arrange
        var plugin = new TidalarrPlugin();
        var settings = new Dictionary<string, object?>
        {
            ["ConfigPath"] = "/config",
            ["RedirectUrl"] = "https://tidal.com/callback",
            ["DownloadPath"] = "/downloads",
            ["EnableCache"] = "false", // Bool as string - line 261: string s when bool.TryParse
            ["ExtractFlac"] = "true"
        };

        // Act
        var result = plugin.ApplySettingsWithDiagnostics(settings);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void ApplySettingsWithDiagnostics_WithEnumAsInt_MapsCorrectly()
    {
        // Arrange
        var plugin = new TidalarrPlugin();
        var settings = new Dictionary<string, object?>
        {
            ["ConfigPath"] = "/config",
            ["RedirectUrl"] = "https://tidal.com/callback",
            ["DownloadPath"] = "/downloads",
            ["PreferredQuality"] = 3 // HiRes as int - lines 283-290: int value handling
        };

        // Act
        var result = plugin.ApplySettingsWithDiagnostics(settings);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void ApplySettingsWithDiagnostics_WithLongAsInt_MapsCorrectly()
    {
        // Arrange
        var plugin = new TidalarrPlugin();
        var settings = new Dictionary<string, object?>
        {
            ["ConfigPath"] = "/config",
            ["RedirectUrl"] = "https://tidal.com/callback",
            ["DownloadPath"] = "/downloads",
            ["CacheDuration"] = 30L // long value - line 243: long l when in int range
        };

        // Act
        var result = plugin.ApplySettingsWithDiagnostics(settings);

        // Assert
        Assert.True(result.IsSuccess);
    }

    #endregion

    #region Validation Edge Cases

    [Fact]
    public void ValidateSettingsWithDiagnostics_WithOutOfRangeCacheDuration_ReturnsFailure()
    {
        // Arrange
        var plugin = new TidalarrPlugin();
        var settings = CreateValidSettings();
        settings["CacheDuration"] = 2000; // Out of range (0-1440) - line 122-125

        // Act
        var result = plugin.ValidateSettingsWithDiagnostics(settings);

        // Assert
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void ValidateSettingsWithDiagnostics_WithOutOfRangeDownloadDelay_ReturnsFailure()
    {
        // Arrange
        var plugin = new TidalarrPlugin();
        var settings = CreateValidSettings();
        settings["DownloadDelay"] = 70000; // Out of range (0-60000) - line 131-134

        // Act
        var result = plugin.ValidateSettingsWithDiagnostics(settings);

        // Assert
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void ValidateSettingsWithDiagnostics_WithInvalidMaxConcurrentTrackDownloads_ReturnsFailure()
    {
        // Arrange
        var plugin = new TidalarrPlugin();
        var settings = CreateValidSettings();
        settings["MaxConcurrentTrackDownloads"] = 5; // Out of range (1-3) - line 136-139

        // Act
        var result = plugin.ValidateSettingsWithDiagnostics(settings);

        // Assert
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void ValidateSettingsWithDiagnostics_WithInvalidMaxConcurrentChunkDownloads_ReturnsFailure()
    {
        // Arrange
        var plugin = new TidalarrPlugin();
        var settings = CreateValidSettings();
        settings["MaxConcurrentChunkDownloads"] = 10; // Out of range (1-8) - line 141-144

        // Act
        var result = plugin.ValidateSettingsWithDiagnostics(settings);

        // Assert
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void ValidateSettingsWithDiagnostics_WithRedirectUrlWrongDomain_ReturnsFailure()
    {
        // Arrange
        var plugin = new TidalarrPlugin();
        var settings = new Dictionary<string, object?>
        {
            ["ConfigPath"] = "/config",
            ["RedirectUrl"] = "https://evil.com/callback", // Not tidal.com - line 108-109
            ["DownloadPath"] = "/downloads"
        };

        // Act
        var result = plugin.ValidateSettingsWithDiagnostics(settings);

        // Assert
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void ValidateSettingsWithDiagnostics_WithInvalidEarlyReleaseLimit_ReturnsFailure()
    {
        // Arrange
        var plugin = new TidalarrPlugin();
        var settings = CreateValidSettings();
        settings["EarlyReleaseLimit"] = 400; // Out of range (0-365) - line 116-120

        // Act
        var result = plugin.ValidateSettingsWithDiagnostics(settings);

        // Assert
        Assert.False(result.IsSuccess);
    }

    #endregion

    #region Helper Methods

    private static IPluginContext CreateMockContext()
    {
        var mock = new Mock<IPluginContext>();
        mock.SetupGet(c => c.HostVersion).Returns(new Version(1, 0, 0));
        mock.SetupGet(c => c.LoggerFactory).Returns(Mock.Of<ILoggerFactory>());
        mock.SetupGet(c => c.Services).Returns(null as IServiceProvider);
        return mock.Object;
    }

    private static Dictionary<string, object?> CreateValidSettings()
    {
        return new Dictionary<string, object?>
        {
            ["ConfigPath"] = "/config/tidalarr",
            ["RedirectUrl"] = "https://tidal.com/callback",
            ["DownloadPath"] = "/downloads/tidal",
            ["TidalMarket"] = "US",
            ["PreferredQuality"] = "Lossless",
            ["EnableCache"] = true,
            ["CacheDuration"] = 15,
            ["EarlyReleaseLimit"] = 14,
            ["IncludeMqa"] = true,
            ["ExtractFlac"] = true,
            ["ReEncodeAAC"] = false,
            ["SaveSyncedLyrics"] = true,
            ["UseLRCLIB"] = false,
            ["DownloadDelay"] = 0,
            ["MaxConcurrentTrackDownloads"] = 2,
            ["MaxConcurrentChunkDownloads"] = 2
        };
    }

    #endregion
}
