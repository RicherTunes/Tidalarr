# Shared Architecture Goals & Change Map

## Guiding Principles
1. **Single orchestration surface** – Both plugins expose only lightweight Lidarr adapters while orchestration, quality mapping, and resilience logic live in Lidarr.Plugin.Common.
2. **Single-package deployment** – MSBuild targets ensure the build output is Lidarr.Plugin.<Name>.dll + PDB + plugin.json with no stray dependencies.
3. **Consistent configuration model** – Settings classes use shared validation helpers and strong enums located in the common library; plugin-specific toggles are layered on top.
4. **Reusable CLI/test harness** – A unified CLI/test infrastructure (command definitions, smoke tests, host probes) prevents drift between plugins.
5. **Strict-but-realistic quality gates** – Agree on warnings-as-errors once legacy gaps are addressed, and standardise analyzer settings to catch regressions early.

## Target Architecture Components
| Layer | Shared (Lidarr.Plugin.Common) | Tidalarr responsibilities | Qobuzarr responsibilities |
| --- | --- | --- | --- |
| **Service registration** | Introduce StreamingIntegrationModule template providing DI registration, CreateIndexer, CreateDownloadClient, CreateOrchestrator hooks. | Refactor src/Tidalarr/Integration/TidalModule.cs to inherit the shared template and override only Tidal specifics. | Replace src/Qobuzarr/src/Services/* bootstrap with the shared template; migrate bespoke helpers into the common library or plugin overrides. |
| **Quality & settings** | Extract shared enums (StreamingQualityLevel), validators, and download/indexer settings base. | Map TidalQuality to shared constructs; keep audio-specific options via partial classes. | Replace QobuzSettings with shared base + Qobuz add-ons; drop duplicated caching configuration helpers. |
| **HTTP/auth/resilience** | Centralise OAuth/token refresh, rate limiting, cached HTTP handlers, and chunk download orchestration. | Consume shared handlers; remove redundant implementations inside Infrastructure/. | Migrate equivalents (RequestSigner, Cache, StreamAvailability) into common library; plugin keeps service-specific tweaks. |
| **Packaging & deployment** | Publish PluginPackaging.targets (ILRepack, cleanup, deploy) consumed by both csproj files. | Remove bespoke target definitions from src/Tidalarr/Tidalarr.csproj and import shared targets; expose PluginExtraContent item for manifest extras. | Delete large ILRepack section in Qobuzarr.csproj and import shared targets; opt-in to extra file copy for ML patterns via PluginExtraContent. |
| **CLI/test harness** | Add StreamingCliHost and PluginHostSmokeTest utilities. | Rebase TidalCLI commands to the shared host; update smoke tests to call the shared harness. | Reuse CLI/test helpers; remove duplicate queue/metadata stubs inside QobuzCLI.Tests. |

## Change Map & Dependencies
1. **Create shared MSBuild infrastructure** (Lidarr.Plugin.Common)
   - Deliver uild/PluginPackaging.targets exported via Directory.Build.props.
   - Provide PluginManifest.props to standardise manifest generation (template optional).
   - Consumers import via <Import Project="..\..\ext\Lidarr.Plugin.Common\build\PluginPackaging.targets" />.
2. **Refactor DI modules**
   - Add new base module (StreamingIntegrationModule) with overridable hooks for settings + adapters.
   - Tidalarr: slim TidalModule to inherit and register only Tidal-specific services.
   - Qobuzarr: collapse to same pattern; migrate queue/metdata/resilience helpers either to common or to a dedicated plugin sub-namespace.
3. **Unify quality/settings model**
   - Introduce shared enums and converters in Lidarr.Plugin.Common.Models.
   - Update Tidalarr TidalDownloadSettings to rely on shared base properties (quality/resilience) and keep advanced options.
   - Rework Qobuzarr settings to remove duplicate validators and align UI labels/attributes.
4. **Shared CLI/test utilities**
   - Move CLI composition (service provider setup, orchestrator creation) into common library.
   - Provide StreamingCliSmokeTests covering auth, search, download flows via dependency injection.
   - Rework both plugins’ CLI/test projects to consume the shared package.
5. **Warnings/analyzers alignment**
   - Publish shared .editorconfig/analyzer settings through common repo.
   - Flip TreatWarningsAsErrors in Qobuzarr after refactor; ensure both builds suppress the same necessary warnings only via central config.

## Sequencing & Constraints
1. Prepare shared MSBuild targets and CLI/test utilities in a feature branch of Lidarr.Plugin.Common; validate with a sample plugin harness before touching Tidalarr/Qobuzarr.
2. Migrate Tidalarr first (smaller surface) to confirm the new targets; update documentation and ensure dotnet build/test still succeed.
3. Port Qobuzarr once shared pieces are stable; tackle module refactor and settings alignment incrementally to avoid large bang changes.
4. After both plugins compile/tests pass under the new structure, enable ILRepack in Qobuzarr and tighten warnings/analyzers.

This blueprint satisfies Step 2 of the alignment effort: we have shared architecture goals with explicit changes mapped to each repository and the common library, ready for staged implementation.
