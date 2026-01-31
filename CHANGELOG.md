# Changelog

All notable changes to Tidalarr will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- **Z.AI GLM Provider**: New AI provider supporting Zhipu AI's GLM-4 series models (via Common library integration)
- **Structured Logging**: Integrated structured logging into core services with Microsoft.Extensions.Logging
- **Logging Extensions**: Added comprehensive structured logging extensions for better observability
- **PrimaryArtistId Plumbing**: Added PrimaryArtistId field from Tidal API to domain models
- **Quality Detection Enhancement**: Enhanced quality detection from Tidal API's `audioQuality` field
- **HostBridge Download Settings**: Complete parity of download settings in HostBridge integration path
- **Technical Debt Documentation**: Added TECH_DEBT_ANALYSIS_2025.md for tracking technical debt

- **Parity Lint CI Job**: Added SHA visibility and fail-fast checks for Common library alignment
- **Stale Artifact Guard**: Added configurable threshold for detecting stale CI artifacts
- **REQUIRE_PACKAGE_TESTS Enforcement**: CI gate enforcing package tests pass before allowing merges
- **Submodule Pinning**: Added `ext-common-sha.txt` for reproducible Common library builds
- **Track Identity Parity Tests**: Characterization tests for track identity validation
- **Fail-Fast Submodule Assert**: Added to all CI workflows

### Changed

- **Provider Consolidation**: Refactored 4 cloud providers (DeepSeek, Groq, OpenRouter, Perplexity) to inherit from `HttpChatProviderBase`
- **Artist ID Normalization**: Normalize empty/null artists to "Unknown Artist" and fix artistId naming conventions
- **Hi-Res Size Estimates**: Differentiate Hi-Res FLAC size estimates from Lossless quality in indexer
- **NLog Telemetry**: Switched to NLog for plugin context compatibility (away from custom telemetry)
- **Common Library**: Multiple bumps for streaming decoder support, sanitizer improvements, metadata tagging

- **OAuth PKCE Flow**: Complete Lidarr-native OAuth implementation with sessionId fix
- **OAuth AuthUrl Field**: Restored OAuthAuthUrl field with file-based implementation for better UX
  - Field remains for UX: users can copy auth URL without digging through logs
  - Getter creates fresh PKCE state file if missing/expired
  - Important for reliable "plugin is loaded" signal in schema
- **Download Protocol**: Set DownloadProtocol correctly for indexer/download client

- **Submodule Pinning**: Adopted ext-common-sha.txt for submodule reproducibility

- **Path Sanitization**: Migrated to use `Sanitize.PathSegment()` for file name sanitization

### Fixed

- **Test Isolation**: Fixed FormatPreferenceCache test isolation and Windows build file locks
- **Cross-Platform Tests**: Made PathValidationExtensions tests cross-platform compatible
- **Package Metadata**: Fixed package size bounds to be configurable
- **API DTO Deserialization**: Added missing FlexibleLongJsonConverter for proper DTO handling
- **Quality Handler**: Fixed Low quality handler after Common library updates
- **Post-processor ILogger**: Avoided ILogger DI in post-processor to prevent injection issues
- **Download Token Persistence**: Fixed Lidarr-native queue tracking persistence
- **Indexer Zero-Byte Fix**: Avoid 0B size when tracks empty
- **Plugin Smoke Test**: Skip when build missing
- ** tidal:// HTTP Fix**: Avoid tidal:// protocol in HTTP requests
- **DTO Id Types**: Corrected Tidal API DTO id types for deserialization

### Refactoring

- **check-host-versions.ps1**: Migrated to Common library module for reusability
- **E2E Infrastructure**: Dedeuped to use Common workflows

### Testing / CI

- Added track identity parity characterization tests
- Added cross-platform path validation tests
- Adopted shared test-runner module from Common library
- Added packaging compliance tests
- Used runsettings for coverage collection on Linux
- Removed redundant Final status gate causing failures
- Improved coverage file selection in status gates
- Standardized Lidarr Docker baseline across workflows

### Documentation

- Added technical debt tracking with registry format
- Clarified artist ID availability in mapper comments
- Added ecosystem E2E infrastructure roadmap
- Aligned ecosystem E2E plan with .NET 8 host baseline
- Expanded OAuth field documentation with multi-plugin stability note

