# Changelog

All notable changes to Tidalarr will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Dependencies
- `ext/Lidarr.Plugin.Common` bumped to **v1.16.0** (`936556e`) — picks up `SlidingWindowAuthFailureHandler` (K-of-N-in-W sliding-window circuit semantics, sibling of `DefaultAuthFailureHandler`) and the rest of the wave-22 Common surface. Tidalarr does not consume `SlidingWindowAuthFailureHandler` directly — brainarr's `LlmAuthCircuit` is the canonical consumer — but the version bump keeps the ecosystem lockstep.
- `ext-common-sha.txt` aligned with the submodule pin: `38eda2c` → `936556e`. Closes the stale-pointer drift surfaced by the Wave-22 adversarial review.

### Build / cleanup
- `.gitignore` extended with `*.net8.0.zip`, `package-release/`, `release-notes.md` so release-build artifacts no longer pollute the working tree.

### Added
- `AuthFailureGate` singleton registered in `TidalModule` — wraps the bridge-default `IAuthFailureHandler` registered by `AddBridgeDefaults()` so the indexer, download client, and OAuth service share one latch state. Mirrors apple + qobuz adoption (`AppleMusicarrStreamingPlugin.cs:130-134`, `QobuzarrStreamingPlugin.cs:36`). Closes the long-standing comment-only reference at `TidalModule.cs:59` ("independent of AuthFailureGate") that left Lidarr's search loop free to hammer `api.tidal.com` on a dead session — the qobuzarr-incident class where a user got IP-banned after auth expired.
- Per-entry-point gate wiring in `TidalLidarrIndexer` + `TidalLidarrDownloadClient` via private static helpers (`IsAuthShortCircuited` + `RecordAuthOutcomeFromException` + `LooksLikeAuthFailure`) that mirror apple's `AppleMusicIndexerAdapter.cs:63-104` pattern. The helpers resolve `AuthFailureGate?` from the runtime's `IServiceProvider` per-call because Lidarr's `HttpIndexerBase` / `DownloadClientBase` ctor signatures are fixed and can't accept additional DI parameters.

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

## [1.2.5] - 2026-05-24

### Added
- `HostBridgeRuntimeCache` retrofit — credential-change invalidation flushes the cached bridge context immediately; a 60 s graveyard holds stale entries to prevent in-flight requests from hard-failing (Wave 13A).
- `PluginLogContext` + `Scrub` observability adopted at 5 entry points — structured per-request correlation and log redaction across Indexer and DownloadClient pipelines (Wave 13C).

### Changed
- Stale credential edge case eliminated: cache no longer returns a bridge context built from superseded OAuth tokens after a settings save.

[Full diff](https://github.com/RicherTunes/Tidalarr/compare/v1.2.4...v1.2.5)

## [1.2.4] - 2026-05-24

### Changed
- `AlbumReleaseInfoBuilder` adopted — unified `ReleaseInfo` string construction replaces two hand-rolled format sites in `TidalLidarrIndexer` (lift wave A item 8).
- `HostBridgeDownloadOrchestrator` adopted in `TidalLidarrDownloadClient` — settings-snapshot + tracked-enqueue; fixes ProbeOnly race where an in-flight snapshot could observe partial settings writes (lift wave A item 2).

### Dependencies
- Common submodule bumped to v1.11.0.

[Full diff](https://github.com/RicherTunes/Tidalarr/compare/v1.2.3...v1.2.4)

## [1.2.3] - 2026-05-24

### Fixed
- `DownloadClient.Test()` multi-field failures now surface all errors — `TestValidationBuilder` from Common adopted; fixes latent bug where the first failure silently swallowed subsequent field errors.

### Changed
- `BackendHealthCache` adopted via `TidalBackendHealthHandler` (DelegatingHandler, outermost layer of all 4 HTTP pipelines) — replaces hand-rolled per-plugin copy.
- `HostGateRegistry.Shutdown` called on module dispose.

### Dependencies
- Common submodule bumped to v1.10.0.

[Full diff](https://github.com/RicherTunes/Tidalarr/compare/v1.2.2...v1.2.3)

## [1.2.2] - 2026-05-23

### Fixed
- `invalid_grant` OAuth UX fix — clears consumed/expired authorization code from state and surfaces a clear field-level error message instead of a generic failure.
- `TidalModule.Version` now derived from assembly `InformationalVersionAttribute`; locked by a contract test (TDD).
- `DownloadClient.Test()` adopted `TestValidationBuilder` — accumulates all field-level failures before returning, fixing a latent bug where the first error silently swallowed subsequent field failures.

### Changed
- HostBridge primitives adopted (`HostBridgeDownloadTracker`, `PrefixedReleaseGuidParser`, `PlaceholderSearchUri`) — approximately 120 LOC saved vs hand-rolled equivalents.

### Dependencies
- Common submodule bumped to v1.9.5.

[Full diff](https://github.com/RicherTunes/Tidalarr/compare/v1.2.1...v1.2.2)
