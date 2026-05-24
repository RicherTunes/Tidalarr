# Changelog

All notable changes to Tidalarr will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
