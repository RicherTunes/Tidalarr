# Tidalarr Tech Debt Analysis - 2025

## Executive Summary

This document provides a comprehensive analysis of technical debt in the Tidalarr plugin, building upon existing documentation in [`TECH-DEBT-INVENTORY.md`](TECH-DEBT-INVENTORY.md) and [`TECH_DEBT_BACKLOG.md`](TECH_DEBT_BACKLOG.md). The analysis identifies new debt items and provides a structured remediation plan focused on high-leverage, low-risk improvements.

## Analysis Methodology

1. **Codebase Exploration**: Examined all source files in `src/Tidalarr/` and `src/Tidalarr.HostBridge/`
2. **TODO/FIXME Search**: Identified unresolved TODO comments
3. **Pattern Analysis**: Looked for code duplication, architectural inconsistencies, and maintenance issues
4. **Cross-Reference**: Compared with existing tech debt documentation
5. **Deletion-Focused**: Prioritized changes that delete duplication and remove TODOs

---

## New Tech Debt Identified

### High Priority

#### 1. Settings Display Metadata Duplication

**Location**:
- [`Integration/SettingsDisplay.cs`](src/Tidalarr/Integration/SettingsDisplay.cs)
- [`Integration/TidalIndexerSettings.cs`](src/Tidalarr/Integration/TidalIndexerSettings.cs)
- [`Integration/TidalDownloadClientSettings.cs`](src/Tidalarr/Integration/TidalDownloadClientSettings.cs)
- [`Integration/TidalarrSettings.cs`](src/Tidalarr/Integration/TidalarrSettings.cs)
- [`HostBridge/Settings/TidalIndexerHostSettings.cs`](src/Tidalarr.HostBridge/Settings/TidalIndexerHostSettings.cs)
- [`HostBridge/Settings/TidalDownloadClientHostSettings.cs`](src/Tidalarr.HostBridge/Settings/TidalDownloadClientHostSettings.cs)

**Issue**: Display metadata (labels, order numbers, units, help text) is duplicated across:
- Core settings classes using `FieldDefinition` attributes
- HostBridge settings classes using `FieldDefinition` attributes
- `SettingsDisplay` static class with constants

**Impact**:
- Maintenance burden - changes must be made in multiple places
- Risk of inconsistency between Core and HostBridge settings
- Violates DRY principle

**Recommended Solution** (Low-Risk First Step):
- Move duplicated HelpText strings into `SettingsDisplay.cs` as constants
- Reference constants from both Core and HostBridge attributes
- This deletes duplication immediately with near-zero behavioral risk
- Source generator approach deferred (uncertain ROI, blast-radius amplifier)

---

#### 2. Enum Duplication - TidalQuality (Medium Priority)

**Location**:
- [`Core/Models/TidalQuality.cs`](src/Tidalarr/Core/Models/TidalQuality.cs) (Core enum)
- [`HostBridge/Settings/TidalQualityHost.cs`](src/Tidalarr.HostBridge/Settings/TidalQualityHost.cs) (Host enum)

**Issue**: Two separate enum definitions for the same concept:
- `TidalQuality` in Core (used internally)
- `TidalQualityHost` in HostBridge (used for Lidarr UI)

**Impact**:
- Manual mapping required in [`TidalDownloadClientHostSettings.MapQuality()`](src/Tidalarr.HostBridge/Settings/TidalDownloadClientHostSettings.cs:34-44)
- Risk of mismatch between enum values
- Maintenance burden when adding new quality levels

**Recommended Solution** (Low-Risk First Step):
- Keep both enums for now (removing TidalQualityHost risks saved-settings compatibility)
- Add lockstep test ensuring same names/values exist
- Add tiny shared mapping helper in Tidalarr (not Common)
- Defer full consolidation until compatibility impact is understood

---

#### 3. TODO Comments - Artist ID Mapping (Split into Two Steps)

**Location**: [`Core/Mappers/TidalModelMapper.cs`](src/Tidalarr/Core/Mappers/TidalModelMapper.cs:25, 34, 76)

```csharp
// TODO: Use actual artist ID when available from API
Id = primaryArtistName,  // Using name instead of ID
```

**Issue**: Artist ID is set to artist name instead of actual ID from Tidal API

**Impact**:
- Incorrect artist identification
- Potential issues with artist matching in Lidarr
- Loss of artist uniqueness

**Recommended Solution** (Two-Step Approach):

**Step A (Safe)**: Plumb real ID if already present in API DTO
- Check if Tidal API DTOs expose artist ID
- Update TidalModelMapper to use real ID when available
- Add tests asserting ID is stable and not name placeholder

**Step B (Larger Change)**: If API client doesn't expose it today
- Update API client to expose artist ID
- Update mapper + tests
- This is a bigger change requiring client + mapper + tests coordination

---

#### 4. TODO Comments - Quality Detection

**Location**: [`Application/Services/TidalSearchService.cs`](src/Tidalarr/Application/Services/TidalSearchService.cs:161, 168)

```csharp
// TODO: Load album tracks - for now return basic album info
// TODO: Enhance with actual quality detection from API
List<TidalQuality> enhancedQualities = [TidalQuality.Low, TidalQuality.High, TidalQuality.Lossless];
```

**Issue**:
- `GetAlbumWithTracksAsync()` doesn't load tracks
- Quality detection uses hardcoded list instead of API data

**Impact**:
- Albums returned without track information
- Incorrect quality information shown to users
- Users may select unavailable quality levels

**Recommended Solution**:
- Replace hardcoded qualities in TidalSearchService with API-derived data
- Use Tidal API's `audioQuality` field from DTOs
- Add characterization test with fixture payload
- Add tests for quality detection

---

### Medium Priority

#### 5. Path Validation Parity (Tests Exist, Need CI Re-enable)

**Location**: [`Integration/PathValidationExtensions.cs`](src/Tidalarr/Integration/PathValidationExtensions.cs)

**Issue**: Path validation tests exist at [`tests/Tidalarr.Tests/Unit/PathValidationExtensionsTests.cs`](tests/Tidalarr.Tests/Unit/PathValidationExtensionsTests.cs) but are excluded in CI with `ExcludeHostBridge=true` (line 76: `<Compile Remove="Unit\PathValidationExtensionsTests.cs" />`)

**Impact**:
- Tests not running in CI, potential for regressions
- Comment indicates Windows paths fail on Linux CI

**Recommended Solution**:
- Re-enable existing tests in CI by removing `Compile Remove` line
- Add missing test cases (UNC, long paths, invalid chars, relative paths)
- Consider moving validation to Common library
- Document path validation requirements

---

#### 6. Packaging Dependency-Closure CI Gate (Already Exists, Needs Verification)

**Location**: Build system (CI/CD)

**Issue**: Packaging gates workflow exists at [`.github/workflows/packaging-gates.yml`](.github/workflows/packaging-gates.yml) which calls Common's reusable workflow. The "zip contains forbidden host assemblies" gate may need strengthening in Common.

**Impact**:
- Risk of shipping Lidarr assemblies with plugin if Common gate is insufficient
- Violation of packaging policy
- Potential runtime conflicts

**Recommended Solution**:
- Verify if Common's packaging-gates workflow has adequate assembly allowlist check
- If insufficient, add/strengthen "zip contains forbidden host assemblies" gate in `lidarr.plugin.common/.github/workflows/packaging-gates.yml`
- Ensure every plugin inherits this gate consistently
- This prevents 4 slightly different allowlist scripts across plugins

---

#### 7. Dual-Path Architecture Complexity

**Location**:
- [`Integration/LidarrNative/`](src/Tidalarr/Integration/LidarrNative/)
- [`HostBridge/`](src/Tidalarr.HostBridge/)

**Issue**: Two parallel integration paths:
- LidarrNative wrappers (extends Lidarr base classes)
- HostBridge (abstracts Lidarr dependencies)

**Impact**:
- Increased codebase complexity
- Potential for divergence between paths
- Higher maintenance burden

**Recommended Solution**:
- Document current dual-path architecture rationale
- Evaluate if consolidation is possible
- Create migration plan if consolidation makes sense

---

#### 8. Manual Service Collection Building

**Location**:
- [`Integration/LidarrNative/TidalLidarrIndexer.EnsureServicesInitialized()`](src/Tidalarr/Integration/LidarrNative/TidalLidarrIndexer.cs:42-80)
- [`Integration/LidarrNative/TidalLidarrDownloadClient.EnsureServicesInitialized()`](src/Tidalarr/Integration/LidarrNative/TidalLidarrDownloadClient.cs:48-90)

**Issue**: Manual `ServiceCollection` building in each LidarrNative wrapper

**Impact**:
- Duplicate initialization code
- Potential for inconsistency
- Harder to test

**Recommended Solution**:
- Consider using DI container from host or Common
- Reduce manual ServiceCollection building
- Add tests for service initialization

---

#### 9. HttpClient Creation Duplication

**Location**: [`Integration/TidalIndexer`](src/Tidalarr/Integration/TidalIndexer.cs:34-51)

```csharp
// Manual HttpClient creation with OAuthDelegatingHandler
if (tokenProvider != null)
{
    OAuthDelegatingHandler handler = new(tokenProvider, logger)
    {
        InnerHandler = new HttpClientHandler()
    };
    this._httpClient = new HttpClient(handler) { ... };
}
```

**Issue**: Manual HttpClient creation instead of using DI

**Impact**:
- Duplicate OAuth handler setup
- Inconsistent with TidalModule configuration
- Harder to test

**Recommended Solution**:
- Use HttpClient from DI
- Remove duplicate OAuthDelegatingHandler setup
- Add tests for HTTP client usage

---

### Low Priority

#### 10. HostBridge Settings Incompleteness (Elevated Priority)

**Location**: [`HostBridge/Settings/TidalDownloadClientHostSettings.cs`](src/Tidalarr.HostBridge/Settings/TidalDownloadClientHostSettings.cs)

**Issue**: Missing fields compared to Core settings:
- `IncludeMqa`
- `ExtractFlac`
- `ReEncodeAAC`
- `SaveSyncedLyrics`
- `UseLRCLIB`

**Impact**:
- Incomplete settings in HostBridge
- Users can't configure all options via HostBridge
- User-visible parity drift: "works in one host path but not the other"

**Recommended Solution** (High Priority - Additive + Safe):
- Add missing fields with proper display metadata
- Update `ToCore()` mapping to include new fields
- Add tests for HostBridge settings
- This is additive and safe, reduces confusion

---

#### 11. Submodule Pinning Guard (Already Exists, Needs Verification)

**Location**: Build system (CI/CD)

**Issue**: Submodule pin workflow exists at [`.github/workflows/submodule-pin.yml`](.github/workflows/submodule-pin.yml). Verify/enforce consistently across all workflows.

**Impact**:
- Risk of using outdated Common library if workflows drift
- Submodule drift
- Potential compatibility issues
- False failures if `ext-common-sha.txt` and workflow refs get out of sync

**Recommended Solution**:
- Verify all CI workflows depend on submodule-pin.yml consistently
- Ensure packaging workflows require submodule-pin to complete first
- Document that packaging-gates.yml ref, ext-common-sha.txt, and submodule HEAD must stay aligned
- This prevents false failures from mismatches

---

## Resolved Tech Debt

### Trim Unused Polly Packages

**Status**: RESOLVED

**Evidence**: No references to `Polly` or `TidalResiliencePolicy` found in codebase

**Resolution**: The Polly dependency has been removed and replaced with Common library's `ExecuteWithRetryAsync()` method.

---

## Existing Tech Debt (from Documentation)

### From TECH-DEBT-INVENTORY.md

| Item | Status | Notes |
|------|--------|-------|
| API v1 Dependency | Monitor | API still working, monitor for deprecation |
| Hardcoded Credentials | Monitor | Same as TidalSharp, works for now |
| Incomplete BTS Support | Low Priority | MPD works reliably |
| Basic Error Handling | Medium | Retry logic exists, could be enhanced |
| FFMPEG Dependency | Low | Required for FLAC extraction |
| No Test Coverage (TidalSharp) | Addressed | New test suite added |
| Tightly Coupled Code | Medium | Refactoring ongoing |
| Synchronous File Ops | Low | Most operations now async |
| Adapter Pattern Overhead | Low | Acceptable trade-off |
| No Response Caching | Addressed | `TidalResponseCache` implemented |
| No Rate Limiting | Addressed | `TidalRateLimiter` implemented |
| Limited Configuration | Low | Adequate for v1 |
| No Telemetry | Low | `TidalTelemetry` exists |

### From TECH_DEBT_BACKLOG.md

| Item | Status | Notes |
|------|--------|-------|
| Trim unused Polly | Done | No Polly references found |
| HostBridge mapping tests | Partial | Basic tests exist, edge cases needed |
| Path validation parity | Pending | Tests exist, need CI re-enable |
| Packaging CI gate | Pending | Exists, needs verification |
| Reduce settings duplication | Pending | See item #1 above |
| Multi-target TFM rationale | Pending | Needs documentation |
| Diagnostics JSON contract | Pending | Needs snapshot tests |
| CLI validation hardening | Pending | Needs improvements |
| Observability alignment | Pending | Needs Common coordination |
| Enum mapping helpers | Pending | See item #2 above |
| Submodule pinning guard | Pending | Exists, needs verification |

---

## Remediation Strategy

The remediation plan is organized into 11 PR-sized milestones, each with clear deletion targets and acceptance criteria. This approach minimizes regression risk and focuses on high-leverage changes that delete duplication and remove TODOs.

### Milestone 0 (S, Low Risk): Hygiene

**Deletion Target**: Prevents false failures from SHA mismatches

### Milestone 1 (S, Low Risk): Settings Parity Tidy-Up

**Deletion Target**: Removes duplicated HelpText strings between Core and HostBridge

### Milestone 2 (S, Low Risk): HostBridge Settings Completeness

**Deletion Target**: Removes user-visible parity drift between integration paths

### Milestone 3 (S, Low Risk): Enum Lockstep Tests

**Deletion Target**: Adds guardrail preventing enum drift

### Milestone 4 (S-M, Medium Risk): Artist ID Mapping - Step A

**Deletion Target**: Removes TODO comments for artist ID mapping (safe path)

### Milestone 5 (S, Low Risk): Path Validation Tests Re-enable

**Deletion Target**: Re-enables existing tests in CI

### Milestone 6 (S, Low Risk): Verify Packaging Gates

**Deletion Target**: Ensures packaging violations are prevented

### Milestone 7 (S, Low Risk): Verify Submodule Pin Consistency

**Deletion Target**: Prevents false failures from SHA mismatches

### Milestone 8 (M, Medium Risk): Quality Detection from API

**Deletion Target**: Removes TODO comments for quality detection

### Milestone 9 (M, Medium Risk): Artist ID Mapping - Step A

**Deletion Target**: Removes TODO comments for artist ID mapping

### Milestone 10 (L, Low Risk): Documentation & Minor Improvements

**Deletion Target**: Addresses documentation gaps and low-priority items

---

## PR-Sized Milestones

### Milestone 0 (S, Low Risk): Hygiene

**Objective**: Keep packaging-gates.yml ref, ext-common-sha.txt, and submodule HEAD aligned

**Changes**:
- Document that these three must stay in sync
- Add comment in submodule-pin.yml explaining alignment requirement
- Ensure all packaging workflows require submodule-pin to complete first

**Deletion Target**:
- Prevents false failures from mismatches
- Ensures consistent submodule management

**Acceptance Criteria**:
- Documentation updated
- Alignment requirement documented

**Risk**: Low - Documentation only

---

### Milestone 1 (S, Low Risk): Settings Parity Tidy-Up

**Objective**: Remove duplicated HelpText strings between Core and HostBridge settings

**Changes**:
- Move duplicated HelpText strings into `SettingsDisplay.cs` as constants
- Update `TidalIndexerSettings.cs` to reference constants
- Update `TidalDownloadClientSettings.cs` to reference constants
- Update `TidalarrSettings.cs` to reference constants
- Update `TidalIndexerHostSettings.cs` to reference constants
- Update `TidalDownloadClientHostSettings.cs` to reference constants

**Deletion Target**:
- Deletes ~20 duplicated HelpText strings
- No behavioral change in Core path
- HostBridge UI behavior unchanged

**Acceptance Criteria**:
- Build/tests green
- No behavioral change in Core path
- HostBridge UI exposes same options
- SettingsDisplay.cs contains all shared strings

**Risk**: Low - Near-zero behavioral risk, only string consolidation

---

### Milestone 2 (S, Low Risk): HostBridge Settings Completeness

**Objective**: Add missing fields to HostBridge settings for user-visible parity

**Changes**:
- Add `IncludeMqa` field to `TidalDownloadClientHostSettings.cs`
- Add `ExtractFlac` field to `TidalDownloadClientHostSettings.cs`
- Add `ReEncodeAAC` field to `TidalDownloadClientHostSettings.cs`
- Add `SaveSyncedLyrics` field to `TidalDownloadClientHostSettings.cs`
- Add `UseLRCLIB` field to `TidalDownloadClientHostSettings.cs`
- Update `ToCore()` mapping to include new fields
- Add tests for HostBridge settings

**Deletion Target**:
- Deletes user-visible parity drift
- Reduces "works in one host path but not the other" confusion

**Acceptance Criteria**:
- Build/tests green
- HostBridge settings match Core settings fields
- All fields properly mapped in `ToCore()`
- Tests cover new fields

**Risk**: Low - Additive change, no breaking changes

---

### Milestone 3 (S, Low Risk): Enum Lockstep Tests

**Objective**: Add guardrail preventing enum drift between Core and HostBridge

**Changes**:
- Add lockstep test ensuring `TidalQuality` and `TidalQualityHost` have same names/values
- Add tiny shared mapping helper in Tidalarr (not Common)
- Update `TidalDownloadClientHostSettings.MapQuality()` to use helper
- Add tests for enum mapping helper

**Deletion Target**:
- Adds guardrail for future enum changes
- Removes manual switch statement maintenance burden

**Acceptance Criteria**:
- Build/tests green
- Lockstep test fails if enums diverge
- Mapping helper replaces manual switch
- Tests cover enum mapping

**Risk**: Low - Only adds tests and helper, no behavioral change

---

### Milestone 4 (S-M, Medium Risk): Artist ID Mapping - Step A

**Objective**: Use real artist IDs when available from API DTOs

**Changes**:
- Check Tidal API DTOs for artist ID field
- Update `TidalModelMapper.ToStreamingArtist()` to use real ID when available
- Update `TidalModelMapper.ToStreamingAlbum()` to use real ID when available
- Update `TidalModelMapper.ToStreamingTrack()` to use real ID when available
- Add tests asserting ID is stable and not name placeholder
- Remove TODO comments in `TidalModelMapper.cs`

**Deletion Target**:
- Deletes 3 TODO comments in `TidalModelMapper.cs`
- Improves artist identification correctness

**Acceptance Criteria**:
- Build/tests green
- Artist ID is not a name placeholder when API provides ID
- Tests verify ID stability
- TODO comments removed

**Risk**: Medium - Depends on API DTO structure, but safe if ID is available

**Note**: If API DTOs don't expose artist ID, this becomes a larger change requiring API client update (Step B)

---

### Milestone 5 (S, Low Risk): Path Validation Tests Re-enable

**Objective**: Re-enable existing path validation tests in CI and add missing cases

**Changes**:
- Remove `<Compile Remove="Unit\PathValidationExtensionsTests.cs" />` from test project (line 76)
- Add missing test cases (UNC, long paths, invalid chars, relative paths)
- Update tests for cross-platform compatibility

**Deletion Target**:
- Re-enables existing tests in CI
- Adds missing test coverage

**Acceptance Criteria**:
- Build/tests green
- Path validation tests run in CI
- All edge cases covered

**Risk**: Low - Tests already exist, just re-enabling and adding cases

---

### Milestone 6 (S, Low Risk): Verify Packaging Gates

**Objective**: Verify Common's packaging-gates workflow has adequate assembly allowlist check

**Changes**:
- Review `lidarr.plugin.common/.github/workflows/packaging-gates.yml`
- Verify "zip contains forbidden host assemblies" gate exists
- If insufficient, add/strengthen gate in Common
- Ensure Tidalarr's packaging-gates.yml inherits properly

**Deletion Target**:
- Ensures packaging violations are prevented
- Prevents 4 slightly different allowlist scripts across plugins

**Acceptance Criteria**:
- Build/tests green
- Packaging gates verified
- All plugins use same gate

**Risk**: Low - Verification only, may require Common coordination

---

### Milestone 7 (S, Low Risk): Verify Submodule Pin Consistency

**Objective**: Verify/enforce submodule pin workflow consistency across all CI workflows

**Changes**:
- Verify all CI workflows depend on submodule-pin.yml consistently
- Ensure packaging workflows require submodule-pin to complete first
- Document that packaging-gates.yml ref, ext-common-sha.txt, and submodule HEAD must stay aligned

**Deletion Target**:
- Prevents false failures from SHA mismatches
- Ensures consistent submodule management

**Acceptance Criteria**:
- Build/tests green
- All workflows verified for consistency
- Documentation updated

**Risk**: Low - Verification and documentation only

---

### Milestone 8 (M, Medium Risk): Quality Detection from API

**Objective**: Replace hardcoded quality list with API-derived data

**Changes**:
- Replace hardcoded qualities in `TidalSearchService.EnhanceAlbumWithQuality()`
- Use Tidal API's `audioQuality` field from DTOs
- Add characterization test with fixture payload
- Add tests for quality detection
- Remove TODO comment in `TidalSearchService.cs`

**Deletion Target**:
- Deletes TODO comment for quality detection
- Deletes hardcoded quality list
- Prevents "promised quality not actually available" regressions

**Acceptance Criteria**:
- Build/tests green
- Quality detection uses API data
- Characterization test with fixture payload
- Tests cover quality detection
- TODO comment removed

**Risk**: Medium - Depends on API data reliability, but improves correctness

---

### Milestone 9 (M, Medium Risk): Artist ID Mapping - Step A

**Objective**: Use real artist IDs when available from API DTOs

**Changes**:
- Check Tidal API DTOs for artist ID field
- Update `TidalModelMapper.ToStreamingArtist()` to use real ID when available
- Update `TidalModelMapper.ToStreamingAlbum()` to use real ID when available
- Update `TidalModelMapper.ToStreamingTrack()` to use real ID when available
- Add tests asserting ID is stable and not name placeholder
- Remove TODO comments in `TidalModelMapper.cs`

**Deletion Target**:
- Deletes 3 TODO comments in `TidalModelMapper.cs`
- Improves artist identification correctness

**Acceptance Criteria**:
- Build/tests green
- Artist ID is not a name placeholder when API provides ID
- Tests verify ID stability
- TODO comments removed

**Risk**: Medium - Depends on API DTO structure, but safe if ID is available

**Note**: If API DTOs don't expose artist ID, this becomes a larger change requiring API client update (Step B)

---

### Milestone 10 (L, Low Risk): Documentation & Minor Improvements

**Objective**: Address documentation gaps and low-priority items

**Changes**:
- Document current TFM choice (Core net8.0, CLI net9.0) in docs
- Add snapshot tests for CFG000/IX200/DL100 error shapes
- Document schema fields/ids in docs
- Improve error messages for invalid enums/args

**Deletion Target**:
- Addresses documentation gaps
- Improves error diagnostics

**Acceptance Criteria**:
- Documentation updated
- Snapshot tests added
- Error messages improved

**Risk**: Low - Documentation and test improvements only

---

## Future Considerations (Not in Current Milestones)

### Artist ID Mapping - Step B (Deferred)
If API DTOs don't expose artist ID in Milestone 9:
- Update API client to expose artist ID
- This is a larger change requiring client + mapper + tests coordination
- Deferred until Milestone 9 reveals necessity

### Packaging Gates - Common Coordination (Deferred)
If Common's packaging-gates workflow needs strengthening:
- Coordinate with @common-api-guardian
- Propose shared assembly allowlist check
- Ensure all plugins inherit same gate

### API v2 Migration (Long-Term)
- Monitor Tidal API v2 stability
- Create migration plan from API v1 to v2
- Document API v1 deprecation risk
- Prepare contingency plan

### Dual-Path Architecture Evaluation (Long-Term)
- Document current dual-path architecture rationale
- Evaluate if consolidation is possible
- Create migration plan if consolidation makes sense
- Deferred until current milestones demonstrate value

---

## Next Steps

1. Review this analysis with the team
2. Begin Milestone 0 implementation (Hygiene)
3. Update documentation as milestones are completed
4. Track deletion targets to ensure progress

---

*Last Updated: 2025-01-24*
