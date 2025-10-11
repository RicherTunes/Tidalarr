HostBridge Integration Guide

Overview
- Core plugin (src/Tidalarr) is hostless: no NzbDrone/Lidarr references; annotations use internal attributes.
- HostBridge (src/Tidalarr.HostBridge) provides host-only models decorated with NzbDrone.Core.Annotations and a mapper to convert to core settings.
- Benefit: CLI/tests run standalone; host UIs still get rich metadata and pretty enum labels.

Registering Host Services
Add the HostBridge DI extension in your host composition root:

```
using Microsoft.Extensions.DependencyInjection;
using Tidalarr.HostBridge;

var services = new ServiceCollection();
services.AddTidalarrHostBridgeServices();
```

What gets registered
- IHostSettingsMapper → HostSettingsMapper (singleton)
  - ToCore(TidalarrHostSettings) → Integration.TidalarrSettings
  - ToCore(TidalIndexerHostSettings) → Integration.TidalIndexerSettings
  - ToCore(TidalDownloadClientHostSettings) → Integration.TidalDownloadClientSettings
  - ToCoreObject(object) → dynamic helper that maps by runtime type

Host Settings vs. Core Settings
- Host-only annotated types (for UI forms):
  - TidalarrHostSettings (implements IIndexerSettings, IProviderConfig)
  - TidalIndexerHostSettings
  - TidalDownloadClientHostSettings (uses TidalQualityHost for SelectOptions)
- Core execution types (hostless, used by the plugin runtime):
  - Integration.TidalarrSettings
  - Integration.TidalIndexerSettings
  - Integration.TidalDownloadClientSettings

Mapping Example
```
using Tidalarr.HostBridge.Settings;

public class SettingsHandler
{
    private readonly IHostSettingsMapper _mapper;
    public SettingsHandler(IHostSettingsMapper mapper) => _mapper = mapper;

    public void Apply(TidalarrHostSettings hostSettings)
    {
        var core = _mapper.ToCore(hostSettings);
        // pass `core` into plugin integration (e.g., TidalarrPlugin.ApplySettingsWithDiagnostics)
    }
}
```

Pretty Enum Labels
- HostBridge exposes TidalQualityHost with FieldOption labels for UI display.
- Download client host settings use `SelectOptions = typeof(TidalQualityHost)`; mapper translates to core enum.

Packaging Notes
- HostBridge is not included in the plugin zip; it exists to support host build-time/runtime UI.
- Core plugin package remains just: Lidarr.Plugin.Tidalarr.dll (+ pdb) and plugin.json, plus Common runtime.

CLI/Tests
- CLI diagnostics (settings/indexer/download) run without host assemblies.
- Optional CLI and packaging tests are under Trait "scope=cli" and are skipped by default.

Migration Tips
- If your host previously used core settings types directly for UI, switch to the HostBridge types and call the mapper to get core models for execution.

Quick Migration Checklist
- Add DI: services.AddTidalarrHostBridgeServices().
- Replace UI-bound settings types from Integration.* with HostBridge.Settings.* types:
  - TidalarrHostSettings (replaces TidalarrSettings for host UI)
  - TidalIndexerHostSettings (replaces TidalIndexerSettings for host UI)
  - TidalDownloadClientHostSettings (replaces TidalDownloadClientSettings for host UI)
- For quality dropdowns, bind to enum TidalQualityHost (pretty labels) instead of the core enum.
- When applying settings, map to core using IHostSettingsMapper and pass core models into plugin services.
- Do not ship HostBridge in the plugin zip; it’s host-only. The core plugin package remains unchanged.

Annotated Host UI Example
```
using NzbDrone.Core.Annotations;
using Tidalarr.HostBridge.Settings;

public class TidalSettingsPanelModel
{
    [FieldDefinition(0, Label = "Redirect URL", Type = FieldType.Textbox)]
    public string RedirectUrl { get; set; } = string.Empty;

    [FieldDefinition(20, Label = "Preferred Quality", Type = FieldType.Select, SelectOptions = typeof(TidalQualityHost))]
    public TidalQualityHost Preferred { get; set; } = TidalQualityHost.Lossless;
}
```

Mapping in Host Code
```
using Tidalarr.HostBridge.Settings;

public class TidalSettingsHandler
{
    private readonly IHostSettingsMapper _mapper;
    public TidalSettingsHandler(IHostSettingsMapper mapper) => _mapper = mapper;

    public void Apply(TidalarrHostSettings host)
    {
        var core = _mapper.ToCore(host);
        // Example: using plugin entrypoint to apply settings with diagnostics
        // var plugin = new Tidalarr.Integration.TidalarrPlugin();
        // await plugin.InitializeAsync(context);
        // var result = plugin.ApplySettingsWithDiagnostics(new Dictionary<string, object?>
        // {
        //     [nameof(core.ConfigPath)] = core.ConfigPath,
        //     [nameof(core.RedirectUrl)] = core.RedirectUrl,
        //     [nameof(core.DownloadPath)] = core.DownloadPath
        // });
    }
}
```

Diagnostics JSON Examples (for tooling/tests)
- Settings success (CFG000):
```
{
  "success": true,
  "value": { "id": "CFG000", "service": "Tidal" },
  "error": null
}
```
- Indexer unauthorized (IX200):
```
{
  "success": false,
  "value": null,
  "error": {
    "code": "Unauthorized",
    "message": "Authentication failed",
    "metadata": { "id": "IX200", "service": "Tidal" }
  }
}
```
- Download stream error (DL100):
```
{
  "success": false,
  "value": null,
  "error": {
    "code": "ProviderUnavailable",
    "message": "Not authenticated",
    "metadata": { "id": "DL100", "trackId": "t1", "quality": "Lossless" }
  }
}
```

CI Notes
- Keep HostBridge out of packaging artifacts; it is referenced only by the host.
- Optional CLI tests live under Trait scope=cli; enable them explicitly on environments with networking and packaging allowed.
