using System.Text.Json;
using Lidarr.Plugin.Abstractions.Contracts;
using Lidarr.Plugin.Abstractions.Manifest;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using Tidalarr.Integration.Adapters;
using Lidarr.Plugin.Abstractions.Results;

namespace Tidalarr.Integration;

public sealed class TidalarrPlugin : IPlugin
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    private readonly TidalModule _module = new();
    private volatile ServiceProvider? _serviceProvider;
    private IPluginContext? _context;
    private TidalarrSettings _settings = new();

    // Non-public Services property for the test harness (accessed via reflection).
    // The volatile field gives visibility across threads; no lock needed.
    private IServiceProvider Services =>
        this._serviceProvider ?? throw new InvalidOperationException("Plugin services not initialized.");

    public PluginManifest Manifest
    {
        get
        {
            try
            {
                string baseDir = AppContext.BaseDirectory;
                string manifestPath = Path.Combine(baseDir, "plugin.json");
                return PluginManifest.Load(manifestPath);
            }
            catch (Exception ex) when (ex is FileNotFoundException or JsonException)
            {
                // Expected: plugin.json missing or malformed — fall back to minimal manifest
                return new PluginManifest
                {
                    Id = "tidalarr",
                    Name = "Tidalarr",
                    Version = "1.0.1",
                    ApiVersion = "1.x",
                    RequiredSettings = ["ConfigPath", "RedirectUrl", "DownloadPath"]
                };
            }
            catch (Exception ex)
            {
                Log.Warn(ex, "Unexpected error loading plugin manifest; using fallback");
                return new PluginManifest
                {
                    Id = "tidalarr",
                    Name = "Tidalarr",
                    Version = "1.0.1",
                    ApiVersion = "1.x",
                    RequiredSettings = ["ConfigPath", "RedirectUrl", "DownloadPath"]
                };
            }
        }
    }

    public ISettingsProvider SettingsProvider { get; }

    public TidalarrPlugin()
    {
        SettingsProvider = new TidalarrSettingsProvider(this);
    }

    public async ValueTask InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
    {
        this._context = context ?? throw new ArgumentNullException(nameof(context));
        // Build initial service provider using default settings to keep the plugin usable before Apply() is called
        RebuildServiceProvider();
        await ValueTask.CompletedTask;
    }

    // Diagnostics-first settings validation (CFG*) using Common result shape
    public PluginOperationResult<Dictionary<string, string>> ValidateSettingsWithDiagnostics(IDictionary<string, object?> settings)
    {
        const string OK = "CFG000";
        const string INVALID = "CFG100";

        TidalarrSettings typed = MapToSettings(settings);
        (bool isValid, var errors) = typed.ValidateSimple();
        if (isValid)
        {
            return PluginOperationResult<Dictionary<string, string>>.Success(new()
            {
                ["id"] = OK,
                ["service"] = "Tidal"
            });
        }

        string[] errorMessages = [.. errors.Select(e => e.Error).Where(e => !string.IsNullOrWhiteSpace(e)).Distinct()];
        Dictionary<string, string> meta = new()
        {
            ["id"] = INVALID,
            ["errors"] = string.Join(",", errorMessages)
        };
        return PluginOperationResult<Dictionary<string, string>>.Failure(new PluginError(PluginErrorCode.ValidationFailed, "Settings failed validation", null, meta));
    }

    public PluginOperationResult<Dictionary<string, string>> ApplySettingsWithDiagnostics(IDictionary<string, object?> settings)
    {
        const string OK = "CFG000";
        const string INVALID = "CFG100";

        TidalarrSettings typed = MapToSettings(settings);
        (bool isValid, var errors) = typed.ValidateSimple();
        if (!isValid)
        {
            string[] errorMessages = [.. errors.Select(e => e.Error).Where(e => !string.IsNullOrWhiteSpace(e)).Distinct()];
            Dictionary<string, string> meta = new()
            {
                ["id"] = INVALID,
                ["errors"] = string.Join(",", errorMessages)
            };
            return PluginOperationResult<Dictionary<string, string>>.Failure(new PluginError(PluginErrorCode.ValidationFailed, "Settings failed validation", null, meta));
        }

        this._settings = typed;
        RebuildServiceProvider();

        return PluginOperationResult<Dictionary<string, string>>.Success(new()
        {
            ["id"] = OK,
            ["service"] = "Tidal"
        });
    }

    public ValueTask<IIndexer?> CreateIndexerAsync(CancellationToken cancellationToken = default)
    {
        IServiceScope scope = Services.GetRequiredService<IServiceScopeFactory>().CreateScope();
        TidalIndexerAdapter adapter = new(scope);
        return ValueTask.FromResult<IIndexer?>(adapter);
    }

    public ValueTask<IDownloadClient?> CreateDownloadClientAsync(CancellationToken cancellationToken = default)
    {
        IServiceScope scope = Services.GetRequiredService<IServiceScopeFactory>().CreateScope();
        TidalDownloadClientAdapter adapter = new(scope);
        return ValueTask.FromResult<IDownloadClient?>(adapter);
    }

    /// <summary>
    /// Atomically swaps the service provider. The old provider is NOT disposed because
    /// concurrent callers (CreateIndexerAsync, CreateDownloadClientAsync) may still hold
    /// a reference obtained from the <see cref="Services"/> getter. Disposing would cause
    /// ObjectDisposedException. The old provider becomes unreachable once all active
    /// operations complete and is collected by the GC. This is safe because all registered
    /// services are managed objects with no unmanaged resource leaks.
    /// </summary>
    private void RebuildServiceProvider()
    {
        this._serviceProvider = this._module.BuildServiceProvider(this._settings);
    }

    public async ValueTask DisposeAsync()
    {
        if (this._serviceProvider is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        }
        else
        {
            this._serviceProvider?.Dispose();
        }
        this._serviceProvider = null;
    }

    private static TidalarrSettings MapToSettings(IDictionary<string, object?> map)
    {
        TidalarrSettings s = new();

        // String properties
        if (GetStringValue(map, nameof(TidalarrSettings.ConfigPath)) is { } configPath)
            s.ConfigPath = configPath;
        if (GetStringValue(map, nameof(TidalarrSettings.RedirectUrl)) is { } redirectUrl)
            s.RedirectUrl = redirectUrl;
        if (GetStringValue(map, nameof(TidalarrSettings.DownloadPath)) is { } downloadPath)
            s.DownloadPath = downloadPath;
        if (GetStringValue(map, nameof(TidalarrSettings.TidalMarket)) is { } market)
            s.TidalMarket = market;

        // Enum property (PreferredQuality)
        if (GetEnumValue<Core.Models.TidalQuality>(map, nameof(TidalarrSettings.PreferredQuality)) is { } quality)
            s.PreferredQuality = quality;

        // Nullable int properties
        if (GetIntValue(map, nameof(TidalarrSettings.EarlyReleaseLimit)) is { } earlyLimit)
            s.EarlyReleaseLimit = earlyLimit;

        // Int properties
        if (GetIntValue(map, nameof(TidalarrSettings.CacheDuration)) is { } cacheDuration)
            s.CacheDuration = cacheDuration;
        if (GetIntValue(map, nameof(TidalarrSettings.DownloadDelay)) is { } delay)
            s.DownloadDelay = delay;
        if (GetIntValue(map, nameof(TidalarrSettings.MaxConcurrentTrackDownloads)) is { } maxTracks)
            s.MaxConcurrentTrackDownloads = maxTracks;
        if (GetIntValue(map, nameof(TidalarrSettings.MaxConcurrentChunkDownloads)) is { } maxChunks)
            s.MaxConcurrentChunkDownloads = maxChunks;

        // Bool properties
        if (GetBoolValue(map, nameof(TidalarrSettings.EnableCache)) is { } enableCache)
            s.EnableCache = enableCache;
        if (GetBoolValue(map, nameof(TidalarrSettings.IncludeMqa)) is { } includeMqa)
            s.IncludeMqa = includeMqa;
        if (GetBoolValue(map, nameof(TidalarrSettings.ExtractFlac)) is { } extractFlac)
            s.ExtractFlac = extractFlac;
        if (GetBoolValue(map, nameof(TidalarrSettings.ReEncodeAAC)) is { } reEncodeAac)
            s.ReEncodeAAC = reEncodeAac;
        if (GetBoolValue(map, nameof(TidalarrSettings.SaveSyncedLyrics)) is { } saveLyrics)
            s.SaveSyncedLyrics = saveLyrics;
        if (GetBoolValue(map, nameof(TidalarrSettings.UseLRCLIB)) is { } useLrclib)
            s.UseLRCLIB = useLrclib;

        return s;
    }

    /// <summary>Extracts a string from raw string or JsonElement.</summary>
    private static string? GetStringValue(IDictionary<string, object?> map, string key)
    {
        if (!map.TryGetValue(key, out object? value) || value is null)
            return null;

        return value switch
        {
            string s => s,
            System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.String } je => je.GetString(),
            _ => null  // Don't silently coerce non-strings
        };
    }

    /// <summary>Extracts an int from raw int or JsonElement.</summary>
    private static int? GetIntValue(IDictionary<string, object?> map, string key)
    {
        if (!map.TryGetValue(key, out object? value) || value is null)
            return null;

        return value switch
        {
            int i => i,
            long l when l >= int.MinValue && l <= int.MaxValue => (int)l,
            System.Text.Json.JsonElement je when je.ValueKind == System.Text.Json.JsonValueKind.Number && je.TryGetInt32(out int i) => i,
            string s when int.TryParse(s, out int i) => i,
            _ => null
        };
    }

    /// <summary>Extracts a bool from raw bool or JsonElement.</summary>
    private static bool? GetBoolValue(IDictionary<string, object?> map, string key)
    {
        if (!map.TryGetValue(key, out object? value) || value is null)
            return null;

        return value switch
        {
            bool b => b,
            System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.True } => true,
            System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.False } => false,
            string s when bool.TryParse(s, out bool b) => b,
            _ => null
        };
    }

    /// <summary>Extracts an enum from raw string, int, or JsonElement.</summary>
    private static T? GetEnumValue<T>(IDictionary<string, object?> map, string key) where T : struct, Enum
    {
        if (!map.TryGetValue(key, out object? value) || value is null)
            return null;

        // Try string first (most common from UI)
        string? strVal = value switch
        {
            string s => s,
            System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.String } je => je.GetString(),
            _ => null
        };
        if (strVal is not null && Enum.TryParse(strVal, ignoreCase: true, out T parsed))
            return parsed;

        // Try integer
        int? intVal = value switch
        {
            int i => i,
            System.Text.Json.JsonElement je when je.ValueKind == System.Text.Json.JsonValueKind.Number && je.TryGetInt32(out int i) => i,
            _ => null
        };
        if (intVal.HasValue && Enum.IsDefined(typeof(T), intVal.Value))
            return (T)(object)intVal.Value;

        return null;
    }

    private sealed class TidalarrSettingsProvider(TidalarrPlugin plugin) : ISettingsProvider
    {
        private readonly TidalarrPlugin _plugin = plugin;

        public IReadOnlyCollection<SettingDefinition> Describe()
        {
            return
            [
                new SettingDefinition
                {
                    Key = nameof(TidalarrSettings.ConfigPath),
                    DisplayName = "Config Path",
                    Description = "Directory used to persist Tidal authentication tokens.",
                    DataType = SettingDataType.String,
                    IsRequired = true
                },
                new SettingDefinition
                {
                    Key = nameof(TidalarrSettings.RedirectUrl),
                    DisplayName = "Redirect URL",
                    Description = "OAuth redirect URL captured after completing the Tidal login flow.",
                    DataType = SettingDataType.String,
                    IsRequired = true
                },
                new SettingDefinition
                {
                    Key = nameof(TidalarrSettings.DownloadPath),
                    DisplayName = "Download Path",
                    Description = "Destination folder for downloaded albums.",
                    DataType = SettingDataType.String,
                    IsRequired = true
                },
                new SettingDefinition
                {
                    Key = nameof(TidalarrSettings.PreferredQuality),
                    DisplayName = "Preferred Quality",
                    Description = "Audio quality requested from Tidal.",
                    DataType = SettingDataType.Enum,
                    AllowedValues = ["Low", "High", "Lossless", "HiRes"],
                    DefaultValue = "Lossless"
                },
                new SettingDefinition { Key = nameof(TidalarrSettings.TidalMarket), DisplayName = "Market", Description = "Two-letter Tidal market code.", DataType = SettingDataType.String, DefaultValue = "US" },
                new SettingDefinition { Key = nameof(TidalarrSettings.EarlyReleaseLimit), DisplayName = "Early Download Limit (informational)", Description = "INFORMATIONAL — stored and round-tripped, but search/download paths do NOT yet apply this window. Wiring is tracked tech debt.", DataType = SettingDataType.Integer, DefaultValue = 14 },
                new SettingDefinition { Key = nameof(TidalarrSettings.EnableCache), DisplayName = "Enable Cache", DataType = SettingDataType.Boolean, DefaultValue = true },
                new SettingDefinition { Key = nameof(TidalarrSettings.CacheDuration), DisplayName = "Cache Duration", Description = "Cache TTL in minutes.", DataType = SettingDataType.Integer, DefaultValue = 15 },
                new SettingDefinition { Key = nameof(TidalarrSettings.IncludeMqa), DisplayName = "Include MQA Masters", DataType = SettingDataType.Boolean, DefaultValue = true },
                new SettingDefinition { Key = nameof(TidalarrSettings.ExtractFlac), DisplayName = "Extract FLAC from M4A", DataType = SettingDataType.Boolean, DefaultValue = true },
                new SettingDefinition { Key = nameof(TidalarrSettings.ReEncodeAAC), DisplayName = "Re-encode AAC Streams (informational)", Description = "INFORMATIONAL — stored but the download pipeline does NOT currently transcode AAC. Tracked tech debt.", DataType = SettingDataType.Boolean, DefaultValue = false },
                new SettingDefinition { Key = nameof(TidalarrSettings.SaveSyncedLyrics), DisplayName = "Save Synced Lyrics", DataType = SettingDataType.Boolean, DefaultValue = true },
                new SettingDefinition { Key = nameof(TidalarrSettings.UseLRCLIB), DisplayName = "Use LRCLIB for Lyrics (informational)", Description = "INFORMATIONAL — stored but the lyrics pipeline does NOT currently call LRCLIB. Tracked tech debt.", DataType = SettingDataType.Boolean, DefaultValue = false },
                new SettingDefinition { Key = nameof(TidalarrSettings.DownloadDelay), DisplayName = "Chunk Delay", Description = "Delay between chunk requests in ms.", DataType = SettingDataType.Integer, DefaultValue = 0 },
                new SettingDefinition { Key = nameof(TidalarrSettings.MaxConcurrentTrackDownloads), DisplayName = "Max Concurrent Track Downloads", DataType = SettingDataType.Integer, DefaultValue = 2 },
                new SettingDefinition { Key = nameof(TidalarrSettings.MaxConcurrentChunkDownloads), DisplayName = "Max Concurrent Chunk Downloads", DataType = SettingDataType.Integer, DefaultValue = 2 }
            ];
        }

        public IReadOnlyDictionary<string, object?> GetDefaults()
        {
            return new Dictionary<string, object?>
            {
                [nameof(TidalarrSettings.ConfigPath)] = string.Empty,
                [nameof(TidalarrSettings.RedirectUrl)] = string.Empty,
                [nameof(TidalarrSettings.DownloadPath)] = string.Empty,
                [nameof(TidalarrSettings.PreferredQuality)] = "Lossless",
                [nameof(TidalarrSettings.TidalMarket)] = "US",
                [nameof(TidalarrSettings.EarlyReleaseLimit)] = 14,
                [nameof(TidalarrSettings.EnableCache)] = true,
                [nameof(TidalarrSettings.CacheDuration)] = 15,
                [nameof(TidalarrSettings.IncludeMqa)] = true,
                [nameof(TidalarrSettings.ExtractFlac)] = true,
                [nameof(TidalarrSettings.ReEncodeAAC)] = false,
                [nameof(TidalarrSettings.SaveSyncedLyrics)] = true,
                [nameof(TidalarrSettings.UseLRCLIB)] = false,
                [nameof(TidalarrSettings.DownloadDelay)] = 0,
                [nameof(TidalarrSettings.MaxConcurrentTrackDownloads)] = 2,
                [nameof(TidalarrSettings.MaxConcurrentChunkDownloads)] = 2
            };
        }

        public PluginValidationResult Validate(IDictionary<string, object?> settings)
        {
            TidalarrSettings typed = MapToSettings(settings);
            (bool isValid, var errors) = typed.ValidateSimple();
            if (isValid) return PluginValidationResult.Success();
            string[] messages = [.. errors.Select(e => e.Error).Where(e => !string.IsNullOrWhiteSpace(e))];
            return PluginValidationResult.Failure(messages);
        }

        public PluginValidationResult Apply(IDictionary<string, object?> settings)
        {
            TidalarrSettings typed = MapToSettings(settings);
            (bool isValid, var errors) = typed.ValidateSimple();
            if (!isValid)
            {
                string[] messages = [.. errors.Select(e => e.Error).Where(e => !string.IsNullOrWhiteSpace(e))];
                return PluginValidationResult.Failure(messages);
            }

            this._plugin._settings = typed;
            this._plugin.RebuildServiceProvider();

            return PluginValidationResult.Success();
        }


    }
}
