# Changelog

All notable changes to Tidalarr will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- `AuthFailureGate` singleton registered in `TidalModule` — wraps the bridge-default `IAuthFailureHandler` registered by `AddBridgeDefaults()` so the indexer, download client, and OAuth service will share one latch state once per-entry-point wiring lands. Mirrors apple + qobuz adoption (`AppleMusicarrStreamingPlugin.cs:130-134`, `QobuzarrStreamingPlugin.cs:36`). Closes the long-standing comment-only reference at `TidalModule.cs:59` ("independent of AuthFailureGate") that left Lidarr's search loop free to hammer `api.tidal.com` on a dead session — the qobuzarr-incident class where a user got IP-banned after auth expired.

### Tests
- `AuthFailureGateAdoptionTests` (4 facts) — verify the DI registration shape (singleton, exactly one, bridge-default handler dependency, factory-or-instance present). Tests inspect `IServiceCollection` by type `FullName` rather than `GetRequiredService<T>()` because the merged Tidalarr.dll's ILRepack-internalized `Lidarr.Plugin.Common` and `Lidarr.Plugin.Abstractions` copies share an FQN with the standalone references the test project uses but have a different assembly identity — direct `typeof(T)` lookup would miss the registration.

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
