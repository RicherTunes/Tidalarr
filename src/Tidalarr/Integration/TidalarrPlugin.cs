using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Lidarr.Plugin.Abstractions.Contracts;
using Lidarr.Plugin.Abstractions.Manifest;
using Microsoft.Extensions.DependencyInjection;
using Tidalarr.Integration.Adapters;
using Tidalarr.Integration.Diagnostics;

namespace Tidalarr.Integration;

public sealed class TidalarrPlugin : IPlugin
{
    private ServiceProvider? _serviceProvider;
    private IPluginContext? _context;
    private TidalarrSettings _settings = new();

    // Non-public Services property for the test harness (accessed via reflection)
    private IServiceProvider Services => _serviceProvider ?? throw new InvalidOperationException("Plugin services not initialized.");

    public PluginManifest Manifest
    {
        get
        {
            try
            {
                var baseDir = AppContext.BaseDirectory;
                var manifestPath = Path.Combine(baseDir, "plugin.json");
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
        _context = context ?? throw new ArgumentNullException(nameof(context));
        // Build initial service provider using default settings to keep the plugin usable before Apply() is called
        RebuildServiceProvider();
        await ValueTask.CompletedTask;
    }

    // Diagnostics-first settings validation (CFG*) for consumers/tests
    internal OperationResult ValidateSettingsWithDiagnostics(IDictionary<string, object?> settings)
    {
        const string OK = "CFG000";
        const string INVALID = "CFG100";

        var typed = MapToSettings(settings);
        var validation = typed.ValidateFluent();
        if (validation.IsValid)
        {
            return OperationResult.Ok(OK, metadata: new() { ["service"] = "Tidal" });
        }

        var codes = validation.Errors
            .Where(e => !string.IsNullOrWhiteSpace(e.ErrorCode))
            .Select(e => e.ErrorCode)
            .Distinct()
            .ToArray();
        return OperationResult.Fail(INVALID, "Settings failed validation", new()
        {
            ["errors"] = codes
        });
    }

    internal OperationResult ApplySettingsWithDiagnostics(IDictionary<string, object?> settings)
    {
        const string OK = "CFG000";
        var check = ValidateSettingsWithDiagnostics(settings);
        if (!check.Success) return check;

        _settings = MapToSettings(settings);
        RebuildServiceProvider();
        return OperationResult.Ok(OK, metadata: new() { ["service"] = "Tidal" });
    }

    public ValueTask<IIndexer?> CreateIndexerAsync(CancellationToken cancellationToken = default)
    {
        var scope = Services.GetRequiredService<IServiceScopeFactory>().CreateScope();
        var adapter = new TidalIndexerAdapter(scope);
        return ValueTask.FromResult<IIndexer?>(adapter);
    }

    public ValueTask<IDownloadClient?> CreateDownloadClientAsync(CancellationToken cancellationToken = default)
    {
        var scope = Services.GetRequiredService<IServiceScopeFactory>().CreateScope();
        var adapter = new TidalDownloadClientAdapter(scope);
        return ValueTask.FromResult<IDownloadClient?>(adapter);
    }

    // Private helper for tests to open a scope (invoked via reflection in PluginSmokeTests)
    private IServiceScope CreateScope()
    {
        return Services.GetRequiredService<IServiceScopeFactory>().CreateScope();
    }

    private void RebuildServiceProvider()
    {
        _serviceProvider?.Dispose();
        var module = new TidalModule();
        _serviceProvider = module.BuildServiceProvider(_settings);
    }

    public async ValueTask DisposeAsync()
    {
        if (_serviceProvider is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        }
        else
        {
            _serviceProvider?.Dispose();
        }
        _serviceProvider = null;
    }

    private static TidalarrSettings MapToSettings(IDictionary<string, object?> map)
    {
        var s = new TidalarrSettings();
        if (map.TryGetValue(nameof(TidalarrSettings.ConfigPath), out var cp) && cp is string cpStr) s.ConfigPath = cpStr;
        if (map.TryGetValue(nameof(TidalarrSettings.RedirectUrl), out var ru) && ru is string ruStr) s.RedirectUrl = ruStr;
        if (map.TryGetValue(nameof(TidalarrSettings.DownloadPath), out var dp) && dp is string dpStr) s.DownloadPath = dpStr;
        if (map.TryGetValue(nameof(TidalarrSettings.PreferredQuality), out var pq))
        {
            if (pq is string pqStr && Enum.TryParse<Tidalarr.Core.Models.TidalQuality>(pqStr, ignoreCase: true, out var parsedEnum))
            {
                s.PreferredQuality = parsedEnum;
            }
            else if (pq is int pqInt && Enum.IsDefined(typeof(Tidalarr.Core.Models.TidalQuality), pqInt))
            {
                s.PreferredQuality = (Tidalarr.Core.Models.TidalQuality)pqInt;
            }
        }
        return s;
    }

    private sealed class TidalarrSettingsProvider : ISettingsProvider
    {
        private readonly TidalarrPlugin _plugin;

        public TidalarrSettingsProvider(TidalarrPlugin plugin) => _plugin = plugin;

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
            var typed = MapToSettings(settings);
            var validation = typed.ValidateFluent();
            return validation.ToPluginValidationResult();
        }

        public PluginValidationResult Apply(IDictionary<string, object?> settings)
        {
            var typed = MapToSettings(settings);
            var validation = typed.ValidateFluent();
            if (!validation.IsValid)
            {
                return validation.ToPluginValidationResult();
            }

            _plugin._settings = typed;
            _plugin.RebuildServiceProvider();
            return PluginValidationResult.Success();
        }

        
    }
}
