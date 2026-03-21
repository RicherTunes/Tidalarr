# Tidalarr Packaging Policy Baseline

This document captures a known-good plugin package output. The source of truth for CI validation is `packaging/expected-contents.txt`.

## Baseline package

- Command: `./build.ps1 -Package -Configuration Release`
- Output: `src/Tidalarr/artifacts/packages/tidalarr-1.0.1-net8.0.zip`

## Expected contents (current)

Required (per `packaging/expected-contents.txt`):
- `Lidarr.Plugin.Tidalarr.dll` (ILRepack-merged plugin assembly)
- `Lidarr.Plugin.Abstractions.dll`
- `plugin.json`

## Other files typically present (not gated by CI)

- `package-metadata.json`
- `*.pdb` (debug symbols; optional for runtime)

Note: Lidarr.Plugin.Common.dll is merged into the main plugin DLL by ILRepack during the packaging step.

## Forbidden contents (should never ship)

Host-provided assemblies (per `packaging/expected-contents.txt`):
- `Lidarr.Core.dll`
- `Lidarr.Common.dll`
- `Lidarr.Http.dll`
- `Lidarr.Api.V1.dll`
- `NzbDrone.Common.dll`
- `NzbDrone.Core.dll`
- `NzbDrone.SignalR.dll`

Other host-provided assemblies (should not ship):
- `NLog.dll`
- `FluentValidation.dll`
- `Microsoft.Extensions.DependencyInjection.Abstractions.dll`
- `Microsoft.Extensions.Logging.Abstractions.dll`
- `System.Text.Json.dll`
