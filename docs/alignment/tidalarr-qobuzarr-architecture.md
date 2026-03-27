> **Note:** This document is historical (dated 2025-09-26) and may not reflect current architecture. Both plugins now target net8.0 (not net6.0 as stated below). See CLAUDE.md for current guidance.

# Tidalarr vs Qobuzarr Architecture Snapshot (2025-09-26)

## Repository Layout & Layering
- **Tidalarr** keeps a layered structure under src/Tidalarr/ with distinct Application, Core, Domain, Infrastructure, Integration, and newly added Lidarr adapters (src/Tidalarr listing). This layout is already aligned with the clean architecture documents in docs/ and centralises new Lidarr-specific adapters under one folder.
- **Qobuzarr** is significantly more fragmented: xt/qobuzarr/src/ contains 20+ top-level folders (API, Authentication, Download, Services, Testing, etc.) plus QobuzarrModule.cs. Many helper abstractions duplicate services that now live in Lidarr.Plugin.Common, indicating historical drift.

## Build & Packaging Configuration
| Aspect | Tidalarr | Qobuzarr | Notes |
| --- | --- | --- | --- |
| Target framework | 
et6.0 (src/Tidalarr/Tidalarr.csproj:4) | 
et6.0 (xt/qobuzarr/Qobuzarr.csproj:4) | Framework parity makes shared library alignment feasible. |
| Warnings policy | Treats warnings as errors (src/Tidalarr/Tidalarr.csproj:6) | Warnings allowed (xt/qobuzarr/Qobuzarr.csproj:22) | Need a consistent policy; consider moving to strict mode once legacy code is cleaned. |
| ILRepack | Always runs after build, pruning extras (src/Tidalarr/Tidalarr.csproj:35-71) | Defined but disabled by default (xt/qobuzarr/Qobuzarr.csproj:61,214) | Tidalarr already ships a single DLL; Qobuzarr still copies many dependencies. |
| Plugin manifest | Static plugin.json linked into output (plugin.json:1-17, src/Tidalarr/Tidalarr.csproj:31-33) | Generated from template replacing {VERSION} (xt/qobuzarr/Qobuzarr.csproj:94-117, xt/qobuzarr/plugin.json.template:1-11) | Decide whether to standardise on generated metadata or static manifests. |
| Deployment target | DeployPluginArtifacts copies only DLL/PDB/manifest (src/Tidalarr/Tidalarr.csproj:72-92) | DeployPlugin copies DLL/PDB/manifest + ML patterns (xt/qobuzarr/Qobuzarr.csproj:260-304) | Need unified deploy target with optional data files. |
| Build scripts | uild.ps1 already mirrors repo guidance (uild.ps1:1-140) | Legacy uild.ps1 still modifies Lidarr sources and leaves ILRepack disabled (xt/qobuzarr/build.ps1:1-120) | Harmonise scripts and remove direct editing of submodule props. |

## Dependency & Shared Library Usage
- Both projects reference the Lidarr.Plugin.Common submodule. Tidalarr brings it in via ProjectReference (src/Tidalarr/Tidalarr.csproj:12) and merges it into the ILRepacked output; Qobuzarr also references it but duplicates logging, caching, and quality services under xt/qobuzarr/src/Services/*.
- Tidalarr recently introduced compile-only NLog to satisfy Lidarr adapters without bundling runtime (src/Tidalarr/Tidalarr.csproj:27). Qobuzarr ships a custom NLogAdapter under src/Abstractions and still distributes NLog binaries.

## Lidarr Integration Surfaces
 - **Tidalarr**: src/Tidalarr/Integration/TidalarrPlugin.cs orchestrates runtime creation of Lidarr-facing adapters (src/Tidalarr/Integration/Adapters/*) so the host only interacts with the new StreamingPlugin bridge.
- **Qobuzarr**: Download/Indexer implementations live under src/Download/Clients and src/Indexers with extensive plugin-specific helpers (queue management, metadata strategies). Alignment requires extracting reusable parts into Lidarr.Plugin.Common and reshaping the per-plugin adapters to mirror the lean approach.

## Settings & Configuration
 - Tidalarr settings use the consolidated TidalarrSettings model (src/Tidalarr/Integration/TidalarrSettings.cs:16) with validators hooked into BaseStreamingSettings.
- Qobuzarr maintains numerous bespoke configuration classes (e.g., src/Qobuzarr/src/Settings/QobuzSettings.cs) with additional ML/queue toggles. Need to reconcile which options belong in the shared library vs plugin-specific layers.

## CLI Harness
- TidalCLI targets .NET 9 and already leverages the new orchestrator (TidalCLI/Program.cs:297-360, 427-436).
- QobuzCLI still mirrors older flows with broader command surface (see xt/qobuzarr/QobuzCLI). CLI convergence will require abstracting shared commands into Lidarr.Plugin.Common and parameterising the service wiring.

## Test Strategy
- Tidalarr tests run on 
et9.0 (	ests/Tidalarr.Tests/Tidalarr.Tests.csproj:4) but currently fail to load the ILRepacked assembly during discovery; the suite needs host probing fixes before we rely on it.
- Qobuzarr tests remain on 
et6.0 and reference many Lidarr host assemblies (xt/qobuzarr/tests/Qobuzarr.Tests/Qobuzarr.Tests.csproj:4-45), increasing maintenance overhead. Future alignment should move to a lighter-weight integration harness similar to Tidalarr’s direction.

## Deployment & Data Artifacts
- Tidalarr deploy target copies only the merged DLL, PDB, and manifest.
- Qobuzarr deploy adds supplementary artifacts like ml-baseline-patterns.json; decide whether those should live in Lidarr.Plugin.Common, stay plugin-specific, or be optional extras triggered via MSBuild property.

## Key Divergences (Actionable)
1. **Packaging**: Qobuzarr’s ILRepack is disabled whereas Tidalarr already produces a single DLL; enable and harmonise the target once dependencies are centralised.
2. **Warnings/Analyzers**: Bring Qobuzarr up to the stricter warning policy and align analyzer configuration with Tidalarr.
3. **DI Modules**: Replace Qobuzarr’s sprawling service registrations with a StreamingPluginModule-style entry point mirroring Tidalarr’s TidalModule (src/Tidalarr/Integration/TidalModule.cs) so both plugins consume Lidarr.Plugin.Common consistently.
4. **CLI/Test Harnesses**: Move toward a shared CLI/test abstraction in Lidarr.Plugin.Common to prevent duplication and ensure both plugins validate via the same patterns.
5. **Manifest Generation**: Pick either template-driven or static manifests; whichever approach we choose should be codified in Lidarr.Plugin.Common build targets so both plugins ingest identical metadata.

This snapshot completes Step 1 of the alignment plan: we have a concrete map of structural, build, and runtime differences that the remaining steps can address without introducing regressions.

