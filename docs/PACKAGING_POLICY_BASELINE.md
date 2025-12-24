# Tidalarr Packaging Policy Baseline

This document captures a known-good plugin package output to support the Phase 1 packaging policy tests in `docs/TESTING_ADOPTION_PLAN.md`.

## Baseline package

- Command: `./build.ps1 -Package -Configuration Release`
- Output: `src/Tidalarr/artifacts/packages/tidalarr-1.0.1-net8.0.zip`

## Expected contents (current)

Required (type identity / runtime deps):
- `Lidarr.Plugin.Abstractions.dll`
- `Microsoft.Extensions.DependencyInjection.Abstractions.dll`
- `Microsoft.Extensions.Logging.Abstractions.dll`

Required (plugin):
- `Lidarr.Plugin.Tidalarr.dll`

Kept (may later be merged/removed, but OK today):
- `Lidarr.Plugin.Common.dll`

Other files currently present:
- `plugin.json`
- `package-metadata.json`
- `*.pdb`, `*.xml` (debug/docs artifacts; optional for runtime)

## Forbidden contents (should never ship)

Host-provided assemblies (examples, non-exhaustive):
- `Lidarr.Core.dll`
- `Lidarr.Common.dll`
- `Lidarr.Host.dll`
- `Lidarr.Http.dll`

Cross-boundary risk:
- `System.Text.Json.dll`
- `FluentValidation.dll` (host-provided; shipping a private copy breaks ValidationFailure type identity)
