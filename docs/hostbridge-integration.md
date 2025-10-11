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

