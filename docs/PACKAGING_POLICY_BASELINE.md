# Tidalarr Packaging Policy Baseline

This document captures a known-good plugin package output. The source of truth for CI validation is `packaging/expected-contents.txt`.

## Baseline package

- Command: `./build.ps1 -Package -Configuration Release` (wraps Common's `New-PluginPackage`; CI uses the same path)
- Output: `src/Tidalarr/artifacts/packages/tidalarr-<version>-net8.0.zip`

## Expected contents (current)

Required (per `packaging/expected-contents.txt`):

- `Lidarr.Plugin.Tidalarr.dll` (ILRepack-merged plugin assembly — internalizes Common + Abstractions)
- `plugin.json`

## Other files typically present (not gated by CI)

- `package-metadata.json`
- `*.pdb` (debug symbols; optional for runtime)

Note: Lidarr.Plugin.Common.dll and Lidarr.Plugin.Abstractions.dll are merged + internalized into the main plugin DLL by ILRepack (see `ext/Lidarr.Plugin.Common/build/PluginPackaging.targets`). Shipping either as a sidecar regresses the merge and triggers `COR_E_INVALIDOPERATION` on multi-plugin installs.

## Forbidden contents (should never ship)

Host-provided assemblies (per `packaging/expected-contents.txt`):

- `Lidarr.Core.dll`
- `Lidarr.Common.dll`
- `Lidarr.Http.dll`
- `Lidarr.Api.V1.dll`
- `Lidarr.Host.dll`
- `NzbDrone.Common.dll`
- `NzbDrone.Core.dll`
- `NzbDrone.SignalR.dll`

Other host-provided assemblies (should not ship):

- `NLog.dll`
- `FluentValidation.dll`
- `Microsoft.Extensions.DependencyInjection.Abstractions.dll`
- `Microsoft.Extensions.Logging.Abstractions.dll`
- `Microsoft.Extensions.Caching.Abstractions.dll`
- `Microsoft.Extensions.Caching.Memory.dll`
- `Microsoft.Extensions.Options.dll`
- `Microsoft.Extensions.Primitives.dll`
- `System.Text.Json.dll`
- `Newtonsoft.Json.dll`

Merged plugin abstractions (must NOT ship as sidecars — they're internalized in the merged plugin DLL):

- `Lidarr.Plugin.Abstractions.dll`
- `Lidarr.Plugin.Common.dll`
