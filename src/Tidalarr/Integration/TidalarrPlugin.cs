using Lidarr.Plugin.Abstractions.Contracts;
using Lidarr.Plugin.Abstractions.Manifest;
using Microsoft.Extensions.DependencyInjection;
using Tidalarr.Integration.Adapters;
using Lidarr.Plugin.Abstractions.Results;

namespace Tidalarr.Integration;

public sealed class TidalarrPlugin : IPlugin
{
    private ServiceProvider? _serviceProvider;
    private IPluginContext? _context;
    private TidalarrSettings _settings = new();

    // Non-public Services property for the test harness (accessed via reflection)
    private IServiceProvider Services => this._serviceProvider ?? throw new InvalidOperationException("Plugin services not initialized.");

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
            catch
            {
                // Fallback minimal manifest to satisfy hosts/tests if plugin.json is not adjacent
                return new PluginManifest
                {
                    Id = "tidalarr",
                    Name = "Tidalarr",
                    Version = "1.0.1",
                    ApiVersion = "1.x",
                    RequiredSettings = new[] { "ConfigPath", "RedirectUrl", "DownloadPath" }
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
        FluentValidation.Results.ValidationResult validation = typed.ValidateFluent();
        if (validation.IsValid)
        {
            return PluginOperationResult<Dictionary<string, string>>.Success(new()
            {
                ["id"] = OK,
                ["service"] = "Tidal"
            });
        }

        string[] codes = validation.Errors
            .Where(e => !string.IsNullOrWhiteSpace(e.ErrorCode))
            .Select(e => e.ErrorCode)
            .Distinct()
            .ToArray();
        Dictionary<string, string> meta = new Dictionary<string, string>
        {
            ["id"] = INVALID,
            ["errors"] = string.Join(",", codes)
        };
        return PluginOperationResult<Dictionary<string, string>>.Failure(new PluginError(PluginErrorCode.ValidationFailed, "Settings failed validation", null, meta));
    }

    public PluginOperationResult<Dictionary<string, string>> ApplySettingsWithDiagnostics(IDictionary<string, object?> settings)
    {
        const string OK = "CFG000";
        PluginOperationResult<Dictionary<string, string>> check = ValidateSettingsWithDiagnostics(settings);
        if (!check.IsSuccess) return check;

        this._settings = MapToSettings(settings);
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
        TidalIndexerAdapter adapter = new TidalIndexerAdapter(scope);
        return ValueTask.FromResult<IIndexer?>(adapter);
    }

    public ValueTask<IDownloadClient?> CreateDownloadClientAsync(CancellationToken cancellationToken = default)
    {
        IServiceScope scope = Services.GetRequiredService<IServiceScopeFactory>().CreateScope();
        TidalDownloadClientAdapter adapter = new TidalDownloadClientAdapter(scope);
        return ValueTask.FromResult<IDownloadClient?>(adapter);
    }

    private void RebuildServiceProvider()
    {
        this._serviceProvider?.Dispose();
        TidalModule module = new TidalModule();
        this._serviceProvider = module.BuildServiceProvider(this._settings);
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
        TidalarrSettings s = new TidalarrSettings();
        if (map.TryGetValue(nameof(TidalarrSettings.ConfigPath), out object? cp) && cp is string cpStr) s.ConfigPath = cpStr;
        if (map.TryGetValue(nameof(TidalarrSettings.RedirectUrl), out object? ru) && ru is string ruStr) s.RedirectUrl = ruStr;
        if (map.TryGetValue(nameof(TidalarrSettings.DownloadPath), out object? dp) && dp is string dpStr) s.DownloadPath = dpStr;
        if (map.TryGetValue(nameof(TidalarrSettings.PreferredQuality), out object? pq))
        {
            if (pq is string pqStr && Enum.TryParse<Core.Models.TidalQuality>(pqStr, ignoreCase: true, out Core.Models.TidalQuality parsedEnum))
            {
                s.PreferredQuality = parsedEnum;
            }
            else if (pq is int pqInt && Enum.IsDefined(typeof(Core.Models.TidalQuality), pqInt))
            {
                s.PreferredQuality = (Core.Models.TidalQuality)pqInt;
            }
        }
        return s;
    }

    private sealed class TidalarrSettingsProvider(TidalarrPlugin plugin) : ISettingsProvider
    {
        private readonly TidalarrPlugin _plugin = plugin;

        public IReadOnlyCollection<SettingDefinition> Describe()
        {
            return new[]
            {
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
                    AllowedValues = new[] { "Low", "High", "Lossless", "HiRes" },
                    DefaultValue = "Lossless"
                }
            };
        }

        public IReadOnlyDictionary<string, object?> GetDefaults()
        {
            return new Dictionary<string, object?>
            {
                [nameof(TidalarrSettings.ConfigPath)] = string.Empty,
                [nameof(TidalarrSettings.RedirectUrl)] = string.Empty,
                [nameof(TidalarrSettings.DownloadPath)] = string.Empty,
                [nameof(TidalarrSettings.PreferredQuality)] = "Lossless"
            };
        }

        public PluginValidationResult Validate(IDictionary<string, object?> settings)
        {
            TidalarrSettings typed = MapToSettings(settings);
            FluentValidation.Results.ValidationResult validation = typed.ValidateFluent();
            return validation.ToPluginValidationResult();
        }

        public PluginValidationResult Apply(IDictionary<string, object?> settings)
        {
            TidalarrSettings typed = MapToSettings(settings);
            FluentValidation.Results.ValidationResult validation = typed.ValidateFluent();
            if (!validation.IsValid)
            {
                return validation.ToPluginValidationResult();
            }

            this._plugin._settings = typed;
            this._plugin.RebuildServiceProvider();
            return PluginValidationResult.Success();
        }


    }
}
