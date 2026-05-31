# Changelog

All notable changes to Tidalarr will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Fixed (download regression — 2026-05-29)
- **All downloads no longer rejected with "resolves outside" when the configured download path has a trailing separator.** `PathTraversalGuard.IsDescendant` (Common) compared the child against a canonical root that still carried a trailing directory separator, producing a doubled separator in the prefix check so *every* legitimate child path failed — Lidarr surfaced this as universal download failure. Fixed upstream in Common #552 (the guard now trims the trailing separator off the canonical root before the prefix comparison) and pulled into tidalarr via the `24b43c1` re-pin below. **Confirmed live** in the Lidarr E2E harness: tidalarr downloaded *A Moon Shaped Pool* with zero "resolves outside" rejections.

### Dependencies
- `ext/Lidarr.Plugin.Common` re-pinned to **`24b43c1`** (2026-05-29) — picks up the PathTraversalGuard trailing-separator fix (#552, see above), the packaging-gates canonical-abstractions opt-in (#549), and the local-ci .NET 8 runtime guardrail (#548). `ext-common-sha.txt` + submodule gitlink advanced together (594a73b → 24b43c1).
- `ext/Lidarr.Plugin.Common` bumped to **v1.17.0** (`639d573`) Wave-23 — picks up the Wave-21 parity helpers (PathTraversalGuard.ContainsTraversalAttempt, AlbumDownloadUri, AlbumReleaseInfoBuilder bracket slots, unified version-bump helper). Tidalarr's own helpers already cover the same ground, but the bump keeps the ecosystem lockstep.
- `ext-common-sha.txt` aligned to `639d573` (was `38eda2c`, then `936556e` after Wave-22).
- `plugin.json` `commonVersion`: 1.16.0 → 1.17.0.

### Build / cleanup
- `.gitignore` extended with `*.net8.0.zip`, `package-release/`, `release-notes.md` so release-build artifacts no longer pollute the working tree.

### Added
- `AuthFailureGate` singleton registered in `TidalModule` — wraps the bridge-default `IAuthFailureHandler` registered by `AddBridgeDefaults()` so the indexer, download client, and OAuth service share one latch state. Mirrors apple + qobuz adoption (`AppleMusicarrStreamingPlugin.cs:130-134`, `QobuzarrStreamingPlugin.cs:36`). Closes the long-standing comment-only reference at `TidalModule.cs:59` ("independent of AuthFailureGate") that left Lidarr's search loop free to hammer `api.tidal.com` on a dead session — the qobuzarr-incident class where a user got IP-banned after auth expired.
- Per-entry-point gate wiring in `TidalLidarrIndexer` + `TidalLidarrDownloadClient` via private static helpers (`IsAuthShortCircuited` + `RecordAuthOutcomeFromException` + `LooksLikeAuthFailure`) that mirror apple's `AppleMusicIndexerAdapter.cs:63-104` pattern. The helpers resolve `AuthFailureGate?` from the runtime's `IServiceProvider` per-call because Lidarr's `HttpIndexerBase` / `DownloadClientBase` ctor signatures are fixed and can't accept additional DI parameters.

### Fixed
- `TidalStreamManifest`: parse failures now emit Warn log entries (was silent swallow) for manifest format drift visibility.

### Changed
- `TidalLidarrIndexer.FetchReleases` short-circuits and returns empty when the gate is latched bad and no probe slot is available — search results are deterministic instead of generating 401-storm log noise.
- `TidalLidarrIndexer.Test` short-circuits with an actionable "auth latched bad" validation failure when the gate has no probe slot — the user sees a clear "paste a fresh redirect URL" path instead of a generic timeout.
- `TidalLidarrDownloadClient.Download` throws an actionable `InvalidOperationException` when the gate is latched bad — Lidarr surfaces this as a download failure with recovery instructions instead of starting a download that will burn API quota / risk IP-ban.
- `TidalLidarrDownloadClient.Test` mirrors the indexer's gate-aware behavior.
- All four entry-point catch blocks now call `RecordAuthOutcomeFromException` so that 401/403 failures latch the gate for subsequent calls.

### Tests
- `AuthFailureGateAdoptionTests` (8 facts):
  - 4 DI-registration facts inspect `IServiceCollection` by type `FullName` rather than `GetRequiredService<T>()` because the merged Tidalarr.dll's ILRepack-internalized `Lidarr.Plugin.Common` and `Lidarr.Plugin.Abstractions` copies share an FQN with the standalone references the test project uses but have a different assembly identity — direct `typeof(T)` lookup would miss the registration.
  - 4 wiring facts use reflection on the merged DLL to verify `TidalLidarrIndexer` + `TidalLidarrDownloadClient` define the private static helpers (`IsAuthShortCircuited`, `RecordAuthOutcomeFromException`) — defense against a future refactor silently removing them.

### Known limitations
- Behavior-level testing (latching the gate and observing indexer short-circuit through real method calls) is blocked by the same cross-ALC issue that breaks the existing `BackendHealthCacheAdoptionTests` (6 pre-existing failures): the test project's standalone `Lidarr.Plugin.Common` / `Lidarr.Plugin.Abstractions` types can't be passed across the merged DLL's internalized boundary. The proper fix is a `bin-tests/` split (qobuzarr-style) where the test project consumes an un-merged Tidalarr.dll — tracked as a separate parity gap.

### Documentation
- CLAUDE.md `## Common helpers in use` section gains `PluginLogContext` (6 confirmed scopes at every canonical entry point: Search, indexer Test, Download, downloadclient Test, OAuthExchange, OAuthRefresh — Tidal has no token-sign path so the apple "auth-token-sign" scope is N/A by design) and `WarnOnce` (documented as not adopted-because-not-needed; repo-wide grep for hand-rolled warn-once patterns returns zero hits). Closes the audit gap of "PluginLogContext partial" and "WarnOnce missing" — both resolve as full coverage / N/A by lack of need.

### Changed (UX — Test() failure messages)
- `TidalLidarrIndexer.Test()` and `TidalLidarrDownloadClient.Test()` catch blocks now route exceptions through `HttpExceptionClassifier` (Common) → categorize as Auth / Network / Timeout / RateLimit / ClientRequest / Server and emit a tailored hint instead of `"Test failed ({CLR-type-name}): {ex.Message}"`. Auth-class failures now surface in the `Authentication` validation field (UI credential section) rather than the generic `Test` bucket. Matches qobuz's adoption pattern at `src/API/AdaptiveQobuzApiClient.cs:54` + `src/Services/AuthTokenManager.cs:376`.

### Changed (parity — class naming)
- `TidalConstants.cs` gains the canonical `PluginName` / `ServiceName` / `PluginVendor` const block matching apple + qobuz convention. Cosmetic parity; no behavior change.
- Class `StreamManifest` → `TidalStreamManifest` (file already `TidalStreamManifest.cs`). Brings the class name in line with peer files in `Tidalarr.Domain.Streaming.*` namespace. References updated across 6 source/test/CLI files; 141 affected tests still green.
- Class `AudioFormatHandler` → `TidalAudioFormatHandler` (file already `TidalAudioFormatHandler.cs`). Same rationale + pattern as the `StreamManifest` rename. References updated across 5 source/test/CLI files.

### Changed (test infrastructure — `bin-tests/` split)
- Test `<ProjectReference>` to `Tidalarr.csproj` now passes `OutputPath=bin-tests\;EnablePluginDeployment=false` alongside the existing `PluginPackagingDisable=true`. The test build now writes an un-merged `Lidarr.Plugin.Tidalarr.dll` (plus standalone `Lidarr.Plugin.Common.dll` + `Lidarr.Plugin.Abstractions.dll`) to `src/Tidalarr/bin-tests/` instead of clobbering the production-merged DLL in `src/Tidalarr/bin/`. Matches qobuzarr's pattern at `tests/Qobuzarr.Tests/Qobuzarr.Tests.csproj:55-60`.
- `PluginSandboxRuntimeTests.FindPluginDll` and `TidalarrPluginLoadFixture.InitializeAsync` updated to look in `bin-tests/` first, falling back to `bin/` for legacy/manual builds.
- `.gitignore` adds `bin-tests/` to the ignore list (matches qobuzarr).
- **Net effect**: 11 previously-failing tests are now green — 6 in `BackendHealthCacheAdoptionTests` (cross-ALC type identity), 4 in `PluginSandboxRuntimeTests` (IPlugin discovery), 1 in `TidalarrPluginSmokeTests` (service resolution). Full suite: 1309 passed / 0 failed / 14 skipped. Closes parity-matrix axis #12 (`bin-tests/` split for cross-ALC type identity).

### Changed (CI — Wave-23)
- `.github/workflows/codeql.yml` + `release.yml`: Docker image pin `ghcr.io/hotio/lidarr:pr-plugins` → `pr-plugins-3.1.2.4913` matching apple+brainarr. Floating tag risk was "works today, breaks silently tomorrow" if hotio cuts a `pr-plugins` rebuild for Lidarr 3.1.3.

## [1.2.9] - 2026-05-29

### Added
- Lyrics enrichment via Common's `LrclibClient` — synced-lyrics (.lrc) fetched alongside audio downloads through LRCLIB public API.
- `DownloadPathValidator` adopted in download client `Test()` — syntactic path validation (traversal, relative, invalid chars) before filesystem probe.
- `GetAlbumWithTracksAsync` now delegates to API client to load tracks — fixes albums without preloaded track data.
- MIT LICENSE file added.

### Fixed
- **Search total failure now surfaces clearly** instead of returning misleading empty result — indexer distinguishes between "no results" and "API failed" states.
- **Search query-bleed bug** — `StreamingApiRequestBuilder` accumulated query parameters across calls, causing all searches to return cached/wrong responses. Fixed upstream in Common v1.12.0 with fail-on-reuse guard.
- **Single-flight token refresh TOCTOU race** — `GetValidTokensAsync` now uses proper single-flight pattern to prevent concurrent refresh attempts.
- **Empty chunk-URL arrays rejected** in `TidalChunkDownloader` — prevents crashes from malformed manifests (TDD).
- **HttpClient leak** — `TidalIndexer` now implements `IDisposable` to properly dispose HTTP client.
- **CancellationToken threaded through** `ValidateChunkAccessibilityAsync` — proper cancellation propagation.
- **InfoUrl security** — album.Id is now URL-encoded to prevent injection (backported from qobuz c9a1574).
- **Deadlock prevention** — sync-over-async migration wrapped in `Task.Run` to prevent UI deadlocks.
- **Performance** — eliminated `Enum.ToString()` allocations in per-album/per-track hot paths.
- **Async efficiency** — added `ConfigureAwait(false)` to 107 bare awaits across 6 plugin files.

### Changed
- **AuthFailureGate consumer helpers adopted** from Common — `ShouldShortCircuit` and `RecordExceptionOutcome` replace local implementations.
- **RateLimitHeaderUtilities** adopted from Common for `Retry-After` header parsing.
- **Common submodule bumped** through multiple waves (v1.14.0 → v1.18.0-dev) — brings Wave-22-28 mega-merge, parity matrix, hot-path hardening.
- **Plugin.json extended** with `owner`, `repository`, `supportUri`, `changelogUri` for ecosystem parity.
- **Docker E2E builds against real Lidarr assemblies** — parity with qobuz/apple/brainarr approach.
- **CI/CD improvements**:
  - Reusable workflow refs migrated from SHA pins to `workflows/v1` tag.
  - Version read from VERSION file instead of removed const.
  - Submodule SHA pins synchronized with verify-pins gate.
  - Host assemblies path handling improved.
- **Security**:
  - Public Tidal protocol constants allowlisted in gitleaks.
  - Interoperability/anti-circumvention disclaimer added.
  - test.trx untracked (leaked machine names/paths).
  - Gitignore hardened for credentials.

### Dependencies
- Common submodule re-pinned to `c2aca69` (AuthFailureGate consumer helpers).
- FluentAssertions 6.12.2 → 8.10.0
- Microsoft.SourceLink.GitHub 8.0.0 → 10.0.300
- coverlet.msbuild 8.0.1 → 10.0.1
- ILRepack.Lib.MSBuild.Task 2.0.44.2 → 2.0.45
- actions/github-script 8 → 9
- actions/setup-dotnet 4 → 5

### Build
- Plugin-load-gate disabled for tidalarr (incompatible with internalize).
- Canonical-abstractions sidecar opt-out in packaging-gates.
- Missing init-common-submodule composite action added.

## [1.2.8] - 2026-05-24

### Changed
- Common submodule bumped v1.12.0 → v1.13.1.
- Sync plugin.json drift after Common bump.

## [1.2.7] - 2026-05-24

### Fixed
- **Search query-bleed hotfix** — `StreamingApiRequestBuilder` accumulated query params across calls, causing all searches to return cached/wrong responses. Fixed upstream in Common v1.12.0 with fail-on-reuse guard.

### Changed
- Sync-over-async guard migrated to Common canonical script.
- Info-log demotion for reduced noise.

## [1.2.6] - 2026-05-24

### Fixed
- **PathTraversalGuard trailing-slash hotfix** — all downloads no longer rejected with "resolves outside" when download path has trailing separator. Fixed upstream in Common #552 and pulled via submodule bump.

### Changed
- Sync-over-async lint guard adopted.

## [1.2.5] - 2026-05-24

### Added
- `HostBridgeRuntimeCache` — credential-change invalidation flushes cached bridge context immediately; 60s graveyard prevents in-flight request failures (Wave 13A).
- `PluginLogContext` + `Scrub` observability at 5 entry points — structured per-request correlation and log redaction across Indexer and DownloadClient pipelines (Wave 13C).

### Changed
- Stale credential edge case eliminated — cache no longer returns bridge context built from superseded OAuth tokens after settings save.

## [1.2.4] - 2026-05-24

### Changed
- `AlbumReleaseInfoBuilder` adopted — unified `ReleaseInfo` string construction replaces two hand-rolled format sites in `TidalLidarrIndexer`.
- `HostBridgeDownloadOrchestrator` adopted in `TidalLidarrDownloadClient` — settings-snapshot + tracked-enqueue fixes ProbeOnly race where in-flight snapshot could observe partial settings writes.

### Dependencies
- Common submodule bumped to v1.11.0.

## [1.2.3] - 2026-05-24

### Fixed
- `DownloadClient.Test()` multi-field failures now surface all errors — `TestValidationBuilder` from Common adopted; fixes latent bug where first failure silently swallowed subsequent field errors.

### Changed
- `BackendHealthCache` adopted via `TidalBackendHealthHandler` (DelegatingHandler, outermost layer of all 4 HTTP pipelines) — replaces hand-rolled per-plugin copy.
- `HostGateRegistry.Shutdown` called on module dispose.

### Dependencies
- Common submodule bumped to v1.10.0.

## [1.2.2] - 2026-05-23

### Fixed
- `invalid_grant` OAuth UX fix — clears consumed/expired authorization code from state and surfaces clear field-level error message instead of generic failure.
- `TidalModule.Version` now derived from assembly `InformationalVersionAttribute`; locked by contract test (TDD).
- `DownloadClient.Test()` adopted `TestValidationBuilder` — accumulates all field-level failures before returning, fixing latent bug where first error silently swallowed subsequent field failures.

### Changed
- HostBridge primitives adopted (`HostBridgeDownloadTracker`, `PrefixedReleaseGuidParser`, `PlaceholderSearchUri`) — ~120 LOC saved vs hand-rolled equivalents.
- Common submodule bumped to v1.9.5.

## [1.2.1] - 2026-05-23

### Fixed
- Common v1.9.3 — Lidarr-Docker token-protection hotfix + adversarial-review hardening.

## [1.2.0] - 2026-05-23

### Added
- `AuthFailureGate` adoption — quality-downgrade detector + Lidarr-native `FetchReleases` gate + HttpClient handler-chain trap closure.
- CI parity-lint VersionContract step + workflow Pester test.
- Documentation: Shared Infrastructure section, CHANGELOG, archived 9 historical plans.
- Security hardening backlog document (10 findings, 2 High).
- Hardcoded-creds ADR + packaging-gates workflow + regression test.
- `HttpExceptionClassifier` adoption in `TidalLidarrDownloadClient.Test()` — categorizes failures with actionable hints.
- TidalRateLimiter wired into every HttpClient — eliminates 429 storms.

### Fixed
- FluentValidation pinned to 9.5.4 (host-coupled AssemblyVersion 9.0.0.0).
- Manifest: dropped deprecated `minimumVersion` (MAN004).
- Non-http(s) redirect URLs rejected (cross-platform parse).
- `MetricsFactoryHttpMessageHandlerFilter` suppressed (ALC trap).
- M.E.Http reverted to 8.0.1 (was 9.0.0 — runtime mismatch).
- Packaging failure made fatal + commonVersion bump.
- Security: enabled `CentralPackageTransitivePinningEnabled`.
- Security: `System.Security.Cryptography.Xml` pinned >= 8.0.3.
- CI smoke-test pinned to specific Common SHA.

### Changed
- Docker E2E harness — tidalarr smoke tests in real Lidarr container.
- Security: PKCEStateStore encrypted at rest via `FileTokenStore<PKCEState>`.
- Coverage: coverlet measures Tidalarr modules numerically.
- Settings: migrated FluentValidation `.Errors.First()` → `.ToString()`.
- Common bump to v1.11.0 for wave-16 security fixes — dropped 2 overrides, applied `[ParityAllowedTokenStore]`.
- Token storage migrated to common's encrypted `FileTokenStore<T>`.
- PKCE routed through common's `PKCEGenerator` — collapsed token providers, added log redaction.
- Common's `ChunkedHttpAssembler` adopted — collapsed two near-duplicate chunk paths.
- Common `PluginConfigRoots` adopted; deleted `ConfigPathDefaults`.

### UX improvements
- TidalDownloadClient PreferredQuality describes tiers + subscription requirements (wave 83).
- OAuth token-exchange messages name stale-redirect cause (wave 79).
- Test() ConfigPath + generic-failure messages match wave 68/72 (wave 73).
- TidalLidarrIndexerSettings ConfigPath error names default location (wave 68).

### Quality improvements
- Quality detection regression coverage for null-tags fix (wave 50).
- TidalQualityDetector null-tags + structured optimizer logging (waves 47, 49).
- CancellationToken propagated through search/album-detail paths (wave 38).
- Optimizer fire-and-forget swallow made explicit (wave 37).

### Infrastructure
- Multi-plugin co-existence support.
- Lidarr.Plugin.*.dll naming contract documented.
- VersionContract parity-lint + workflow Pester test.
- Release packaging failure made fatal.
- CI: Docker E2E job using common composite action.
- CI: consume common's lifted LidarrContainerFixture (wave 22a).

### Dependencies
- Common bumped through multiple versions (v1.5.0 → v1.7.1 → various SHA pins).
- Microsoft.Extensions.{DI,Logging.Abs} bumped to 9.0.0.
- M.E.Http aligned to 9.0 + minimumVersion added.

## [1.1.1] - 2026-05-23

### Fixed
- Release asset named with `net8.0.zip` suffix — required for Lidarr UI install to recognize plugin.
- TidalRateLimiter wired into every HttpClient — eliminates 429 storms.

## [1.1.0] - 2026-05-10

### Added
- Multi-plugin co-existence support.
- Docker E2E coverage cliff fix + sidecar-tolerant scripts in Common.
- FluentValidation pinned to 9.5.4 (host-coupled AssemblyVersion 9.0.0.0).
- M.E.* 8.0 alignment in Common testkit.

### Changed
- Common bumped to 90da1f6 (Abstractions cross-ALC fix) + aligned M.E.* pins.
- Common bumped to 904d5ae.
- Manifest: dropped deprecated `minimumVersion` (MAN004).
- Auto-generated UI screenshots refreshed.
- Non-http(s) redirect URLs rejected (cross-platform parse).
- `MetricsFactoryHttpMessageHandlerFilter` suppressed (ALC trap).
- M.E.Http reverted to 8.0.1 (was 9.0.0 — runtime mismatch).
- Manifest: bumped commonVersion 1.5.0 → 1.7.1.
- ext-common-sha.txt pin updated to 263a182.
- M.E.Http aligned to 9.0 + minimumVersion added.
- Sync-over-async lint: 4 new Category-A sites allowlisted.
- M.E.{DI,Logging.Abs} bumped to 9.0.0.
- Security: `CentralPackageTransitivePinningEnabled` enabled.
- Security: `System.Security.Cryptography.Xml` pinned >= 8.0.3.
- CI smoke-test pinned to Common SHA.

### UX
- TidalDownloadClient PreferredQuality describes tiers + subscription requirements (wave 83).
- OAuth token-exchange messages name stale-redirect cause (wave 79).
- Test() ConfigPath + generic-failure messages match wave 68/72 (wave 73).
- TidalLidarrIndexerSettings ConfigPath error names default location (wave 68).

### Quality
- Quality regression coverage for null-tags fix (wave 50).
- TidalQualityDetector null-tags + structured optimizer logging (waves 47, 49).
- CancellationToken propagated through search/album-detail paths (wave 38).
- Optimizer fire-and-forget swallow made explicit (wave 37).

### Infrastructure
- Docker E2E job wired using common composite action (wave 23).
- Consume common's lifted LidarrContainerFixture (wave 22a).
- Docker-based E2E harness — tidalarr smoke tests in real Lidarr container (wave 21).
- Long-tail coverage gap-fill (wave 20).
- Common bumped to 27cbe1b for wave-16 security fixes.
- PKCEStateStore encrypted at rest via `FileTokenStore<PKCEState>`.
- Coverlet measures Tidalarr modules numerically.
- ObservabilityShim reflective bind-and-invoke path covered.
- Settings migrated FluentValidation `.Errors.First()` → `.ToString()` (wave 11c).
- Common bump to 52a17ed (wave 11) — dropped 2 overrides, applied `[ParityAllowedTokenStore]`.
- Targeted coverage gap-fill (wave 12).
- Behavior-contract parity checks adopted.
- Remaining 4 cov-test failures resolved.
- Pre-existing tidalarr cov-test failures resolved (8 → 0).
- Common's `ChunkedHttpAssembler` adopted — collapsed two near-duplicate chunk paths (phase 5d).
- Common `PluginConfigRoots` adopted; `ConfigPathDefaults` deleted (phase 5b).
- Token storage migrated to common's encrypted `FileTokenStore<T>` (phase 2).
- PKCE routed through common's `PKCEGenerator`; collapsed token providers, added log redaction (phase 1).
- Local work recovered after SSD-crash + data-recovery corruption.
- Auto-generated UI screenshots refreshed.

### Documentation
- Multi-Plugin section updated — fixed 2026-05-10.

## [1.0.1-preview-obs-20251011-1534-911d939] - 2025-10-11

### Added
- CLI hardening: parsing for search/download + gated CLI tests.
- TFM_RATIONALE docs (core net6.0, CLI net9.0), linked in README and docs index.
- Submodule pinning workflow comparing `ext/Lidarr.Plugin.Common` to `ext-common-sha.txt`.
- `TidalResiliencePolicy` + tests removed; Polly dependencies dropped from plugin.
- Tech-debt issue template + `scripts/create-tech-debt-issues.ps1`.
- TECH_DEBT_BACKLOG with prioritized items + acceptance criteria.
- `CliFact` to conditionally skip CLI/packaging tests via `RUN_REAL_CLI_TESTS` env.
- PR template with HostBridge links, testing + packaging checklist.
- docs/README index and README Host vs. Core section with HostBridge link.
- HostBridge migration checklist, UI example, and diagnostics JSON examples.
- README links HostBridge integration guide for host setup.
- HostBridge integration guide with DI registration and mapping examples.
- DI extension `AddTidalarrHostBridgeServices` to register `IHostSettingsMapper`.
- `IHostSettingsMapper` for unified host->core conversion.
- Pretty enum labels + mapping: `TidalQualityHost` enum with NzbDrone `FieldOption` labels.
- `PreferredQuality` to `DownloadClient` host settings with `SelectOptions=typeof(TidalQualityHost)`.
- `ToCore()` mapping for Indexer/Download host settings.
- Host-only settings with NzbDrone annotations: `TidalarrHostSettings` implements `IIndexerSettings`/`IProviderConfig`.
- Core settings made hostless; HostBridge supplies annotated equivalents.
- Hostless annotations (`FieldDefinition`/`FieldOption`/`FieldType`).
- NzbDrone.* usages replaced with local aliases in settings/models.
- `IIndexerSettings`/`IProviderConfig` and `NzbDroneValidationResult` methods removed.
- Internal path validation extension (no host libs).
- Lidarr.Core/Common references dropped from plugin csproj.

### Infrastructure
- CLI diagnostics + packaging tests (Trait scope=cli).
- CLI integration tests for settings/indexer/download validate commands.
- Dependency-closure test on packaged zip.
- Tests opt-in via `[Fact(Skip=...)]` and marked Trait scope=cli.
- Robust repo-root detection + safe process invocations.
- Common `PluginOperationResult` and JSON helper adopted.
- Local `OperationResult` replaced with `Lidarr.Plugin.Abstractions.Results.PluginOperationResult`.
- Generic success payload with diagnostics id; failures mapped to `PluginError` with id/metadata.
- CLI emits Common-shaped JSON for settings/indexer/download validate commands.
- `DiagnosticTapHandler` removed.
- Tests assert Common result shape (value/error + metadata).
- `InternalsVisibleTo` for TidalCLI to access diagnostics helpers.
- PluginOperationResultJson vendored into ext; CLI diagnostics output switched to Common JSON shape.
- Nightly and release workflows; CLI tests filtered by default; submodule bumped to main.
- Diagnostics + resilience rewired for upstream API.
- CLI indexer-validate and download-validate commands (diagnostics JSON).
- HTTP calls aligned with current Common extensions.
- Local diagnostic tap references dropped.
- CLI settings-validate command that prints diagnostics JSON (CFG codes).
- `OperationResult` APIs exposed.
- CFG* diagnostics for aggregated settings (validate/apply) with tests.
- Mapping helper refactored.
- Indexer diagnostics (IX000/IX100/IX200) for settings + init.
- Diagnostics tests added.
- Stable IDs asserted.
- Internals exposed to tests.
- `StreamingTokenManager` with adapter + `ManagedTokenProvider`.
- DI registration and OAuth handler wiring.
- `Tidalarr.HostBridge` project and `TidalProtocol` moved; test project references bridge.
- `TidalStreamingAuthManager` and DI registration added.
- `IPlugin` entry implemented.
- Common `ContentDecodingSnifferHandler` adopted.
- `TidalProtocol` adjusted to match tests.
- Deterministic build flags added.
- Unified plugin guardrails added to CI.
- Universal adaptive rate limiter adopted.
- Latest lidarr.plugin.common submodule update.
- Extensions stack aligned with shared module.
- Streaming DI helpers centralized.
- Shared packaging targets imported.
- Lidarr adapters and alignment docs added.
- Latest shared helper fixes pulled.
- Repo build helper scripts added.
- Lidarr.Plugin.Common submodule updated.
- CLI smoke coverage for core commands added.

### CLI
- Cover art embedded after album download.
- Artist album folders created for downloads.
- README + agent file created.
- Submodule pointer updated to latest common lib.
- CLI quality mapping and config usage refined.
- Warnings-as-errors enforced in plugin and CLI.
- Nullable uses and async stubs fixed.
- CLI defaults and DI calls updated.
- `InputSanitizer` replaced with `Sanitize.DisplayText`.
- Build clean.
- Submodule merge `main-updated` into main and pointer update.
- Auto-open browser on auth-start.
- Configurable defaults (output dir, quality).
- Error messages improved for live commands.
- `TidalModule.RegisterServices` + `CreateOrchestrator` for DI-based composition used.
- DI-like orchestrator construction helper added.
- Live search command added.
- Duplicate switch cases fixed.
- Live OAuth commands (auth-start/auth-complete) added.
- Download commands using shared `SimpleDownloadOrchestrator` + `TidalChunkStreamProvider` added.

### Plugin
- `TidalChunkStreamProvider` DI-registered.
- `CreateOrchestrator` factory using shared `SimpleDownloadOrchestrator` added.
- `TidalChunkStreamProvider` implementing `IAudioStreamProvider` using `TidalStreamService` + `TidalChunkDownloader` added.
- Updated common submodule vendor (orchestrator progress + stream provider).
- Tidal orchestrator construction example added (commented, not built).
- Common submodule vendor (resilience adapter doc + simple orchestrator resume checkpoints).
- Common submodule vendor updated with resilience adapter and simple orchestrator.
- Shared `OAuthDelegatingHandler` with `IStreamingTokenProvider` used.
- Adapter for stubs added.
- API+chunk HTTP switched to shared resilience (429 Retry-After, retry budget, per-host gates).
- Filenames NFC+sanitize.
- Preview filtering.
- Downloads write .partial + atomic move + optional signature validation.
- Quality mapping preserves Tidal IDs while aligning tiers.

### Core
- DASH manifest parser implementation with TidalSharp patterns completed.
- Shared library with merged CLI framework improvements updated.
- Production-first CLI framework architecture integrated.
- Shared library submodule updated with build artifact cleanup.
- Roadmap updated with shared library integration progress.
- Shared library integration with `Lidarr.Plugin.Common` initiated.
- `Lidarr.Plugin.Common` submodule updated with .gitignore improvements.
- Integration with all Tidalarr services and shared library components completed.
- `Lidarr.Plugin.Common` submodule updated to v1.1.0.
- `Lidarr.Plugin.Common` enhanced (v1.1.0).
- Proper integration with `Lidarr.Plugin.Common` submodule.
- Core utilities extracted to `Lidarr.Plugin.Common` library.
- Using statements cleaned up in `EndToEndIntegrationTests`.
- Comprehensive unit test coverage for 100% testing goal added.
- Final build fixes and syntax corrections.
- Telemetry, observability and architect requirements completed.
- Architect feedback implementation - resilience and memory completed.
- Tidalarr MVP implementation with architect feedback integration completed.
