# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Tidalarr is a high-performance Lidarr plugin for Tidal streaming service, built using the Lidarr.Plugin.Common shared library architecture. It provides both indexing and download capabilities for high-quality audio content from Tidal.

## Runtime & Docker Image Requirements (CRITICAL)

**Target framework**: `net8.0` — this plugin MUST target .NET 8.

**Lidarr Docker image**: Use ONLY a `.NET 8` plugins-branch image for CI and local testing. The correct tag format is `pr-plugins-3.x.y.z` (net8). Example:
```
LIDARR_DOCKER_VERSION=pr-plugins-3.1.2.4913
```
- Image: `ghcr.io/hotio/lidarr:pr-plugins-3.1.2.4913`

**NEVER use `pr-plugins-2.x` tags** (e.g., `pr-plugins-2.14.2.4786`) — those are .NET 6 images. Loading a .NET 8 plugin into a .NET 6 host causes `System.Runtime` assembly load failures and Lidarr crash-loops (`Could not load file or assembly 'System.Runtime, Version=8.0.0.0'`).

When bumping the Docker image tag, search the entire repo for the old tag string and update all hits (workflows, scripts, docs).

## Plugin Registration (CRITICAL — controls Lidarr System→Plugins UI visibility)

Lidarr has **two** distinct `IPlugin` interfaces, and conflating them silently breaks the System→Plugins UI:

| Interface | From | Used by |
|---|---|---|
| `NzbDrone.Core.Plugins.IPlugin` | `Lidarr.Core.dll` (host) | `/api/v1/system/plugins` — UI listing, update checks, uninstall |
| `Lidarr.Plugin.Abstractions.IPlugin` | Common (internalized via ILRepack) | TestKit `PluginSandbox` — never read by the live host |

`TidalarrPlugin : IPlugin` (Common's IPlugin) satisfies the bridge contract. `TidalIndexer`/`TidalDownloadClient` are discovered through their Lidarr base classes. Neither satisfies the host's `IPlugin`, so without an additional class the plugin loads fully and works but doesn't appear in System→Plugins (and can't be auto-updated/uninstalled through the UI).

`src/Tidalarr/Integration/TidalarrInstalledPlugin.cs` extends the host's `NzbDrone.Core.Plugins.Plugin` to close the gap:

```csharp
public sealed class TidalarrInstalledPlugin : NzbDrone.Core.Plugins.Plugin
{
    public override string Name => "Tidalarr";
    public override string Owner => "RicherTunes";
    public override string GithubUrl => "https://github.com/RicherTunes/Tidalarr";
}
```

DryIoc's `RegisterMany` (in `NzbDrone.Common.Composition.Extensions.AutoAddServices`) auto-discovers this class from the loaded plugin assembly. `InstalledVersion` is derived from `AssemblyInformationalVersionAttribute` via the base class — do **not** hardcode it. Tidalarr.csproj wires the assembly version from the top-level VERSION file (Directory.Build.props), and the `VersionContractTests` enforce that sources stay in sync.

## Release Asset Naming (CRITICAL — controls Lidarr UI install)

**Every release asset filename MUST contain the literal substring `net8.0.zip`.**

Lidarr's plugin install (UI "Install" on a GitHub URL) is implemented in `src/NzbDrone.Core/Plugins/PluginService.cs` on the `plugins` branch. The asset filter is:

```csharp
release.Assets.Any(a => a.Name.Contains($"{Framework}.zip", StringComparison.OrdinalIgnoreCase))
// where Framework = $"net{_platformInfo.Version.Major}.0"  →  "net8.0"
```

If no asset matches, `GetRemotePlugin` returns `null` and `InstallPluginService.Execute` silently no-ops — **the UI spinner spins forever with no error**. This is the failure mode users see as "Install button does nothing."

Other constraints the install enforces:

- `draft: false`
- `target_commitish` ∈ `{main, master}` (case-insensitive)
- Tag parses as a version (`v1.2.3`, `1.2.3`, or `1.2.3-prerelease`)
- Optional `Minimum Lidarr Version: X.Y.Z.W` in release body must be ≤ host version

Our release zip is named `Lidarr.Plugin.Tidalarr-v<VERSION>.net8.0.zip` (`.github/workflows/release.yml`). Do not rename without keeping the `net8.0.zip` suffix.

**Verify a release is installable:**

```bash
gh api repos/RicherTunes/tidalarr/releases --jq '.[0] | {tag_name, draft, target_commitish, assets: [.assets[].name]}'
```

At least one asset name must contain `net8.0.zip`.

**ALWAYS**:
- Use constants from `TidalConstants.cs` rather than hardcoding.
- Expose to the user what brings value in `TidalDownloadSettings.cs` or `TidalarrSettings.cs`; otherwise, it should be in `TidalConstants.cs`.
- Be aware that this project shares a common library with http://github.com/RicherTunes/Lidarr.Plugin.Common so always think of ways to ensure generic code can be shared with this library so other projects may benefits. Think architecturally when doing so.

## Plugin DLL Naming Contract (CRITICAL)

**The main plugin DLL filename MUST match the glob `Lidarr.Plugin.*.dll`.** Lidarr's PluginLoader (`NzbDrone.Common/Extensions/PathExtensions.cs:334`) scans `/config/plugins/{owner}/{name}/` with `Directory.GetFiles(folder, "Lidarr.Plugin.*.dll")` — any other filename is silently ignored. No error, no warning, no log line; the plugin just never appears in `/api/v1/system/plugins`.

For Tidalarr this is satisfied by `<AssemblyName>Lidarr.Plugin.Tidalarr</AssemblyName>` in `src/Tidalarr/Tidalarr.csproj`. Don't drop that line "to clean up" — it's load-bearing.

## Common helpers in use

- `PluginConfigRoots.Resolve("Tidalarr")` — `src/Tidalarr/Integration/TidalIndexerSettings.cs:15`, `src/Tidalarr/Integration/TidalarrSettings.cs:16`, `src/Tidalarr/Integration/LidarrNative/TidalLidarrIndexerSettings.cs:17`
- `BackendHealthCache` — `src/Tidalarr/Infrastructure/Resilience/TidalBackendHealthHandler.cs:33` (DelegatingHandler wrapping `BackendHealthCache.Shared`)
- `HostBridgeDownloadTrackerStore` — `src/Tidalarr/Integration/LidarrNative/TidalLidarrDownloadClient.cs:38` (static store for in-flight downloads)
- `HostBridgeDownloadOrchestrator` — `src/Tidalarr/Integration/LidarrNative/TidalLidarrDownloadClient.cs:39`
- `PrefixedReleaseGuidParser` — `src/Tidalarr/Integration/LidarrNative/TidalLidarrDownloadClient.cs:363`
- `PlaceholderSearchUri` — `src/Tidalarr/Integration/LidarrNative/TidalLidarrIndexer.cs:139`, `src/Tidalarr/Integration/LidarrNative/TidalLidarrIndexer.cs:438`
- `PathTraversalGuard` — `src/Tidalarr/Integration/LidarrNative/TidalLidarrDownloadClient.cs:401`
- `AlbumReleaseInfoBuilder` — `src/Tidalarr/Integration/LidarrNative/TidalLidarrIndexer.cs:540`, `src/Tidalarr/Integration/LidarrNative/TidalLidarrIndexer.cs:583`
- `TestValidationBuilder` — `src/Tidalarr/Integration/LidarrNative/TidalLidarrDownloadClient.cs:307`

See `ext/Lidarr.Plugin.Common/CHANGELOG.md` for the full catalog.

## Build Commands

### **Development Builds (with CLI tools)**

For development work that requires CLI framework dependencies:

```bash
# Development build with CLI framework
dotnet build -p:IncludeCLIFramework=true

# Development build with specific configuration
dotnet build --configuration Debug -p:IncludeCLIFramework=true

# Restore and build for development
dotnet restore && dotnet build -p:IncludeCLIFramework=true
```

### **Production Builds (clean dependencies)**

For production deployments without pre-release CLI dependencies:

```bash
# Production build (default - clean dependencies)
dotnet build

# Production release build
dotnet build --configuration Release

# Production build with explicit CLI exclusion
dotnet build -p:IncludeCLIFramework=false
```

## CLI Framework Architecture

**🎯 Production-First Approach**: Tidalarr uses an opt-in CLI framework strategy for better production deployments and external adoption.

### **Why This Architecture?**

| Aspect | Benefit |
|--------|---------|
| **Development** | CLI tools available with `-p:IncludeCLIFramework=true` flag |
| **Production** | Clean stable dependencies, no pre-release packages |
| **External Adoption** | Other teams get clean library experience |
| **Scalability** | Sustainable architecture for multiple services |

### **How It Works**

1. **Default Behavior**: Development builds include CLI framework (`IncludeCLIFramework=true`)
2. **Production Override**: Use `-p:IncludeCLIFramework=false` for clean production builds
3. **CLI Project**: TidalCLI always includes CLI framework regardless of flag
4. **Conditional Dependencies**: Shared library only includes System.CommandLine/Spectre.Console when flag is enabled

## Project Structure

```
src/
├── Tidalarr/                 # Main plugin (Lidarr.Plugin.Tidalarr.dll)
│   ├── Core/                 # Core models, interfaces, constants
│   ├── Domain/               # API clients, authentication, streaming
│   ├── Infrastructure/       # Caching, performance, storage
│   ├── Integration/          # Lidarr integration (indexer, download client)
│   └── Application/          # Application services
│
TidalCLI/                     # CLI wrapper for testing and development
├── Commands/                 # CLI command implementations  
├── Services/                 # CLI-specific service adapters
└── Program.cs                # CLI entry point

ext/Lidarr.Plugin.Common/     # Shared library (submodule)
```

## Key Components

### **Plugin Architecture (Plugin-First Design)**
- **TidalIndexer**: Implements `BaseStreamingIndexer<TidalarrSettings>` for Lidarr search integration
- **TidalDownloadClient**: Implements `BaseStreamingDownloadClient<TidalDownloadSettings>` for downloads
- **TidalApiClient**: HTTP client using StreamingApiRequestBuilder pattern
- **TidalModelMapper**: Maps between Tidal models and shared library models
- **TidalResponseCache**: Tidal-specific caching extending StreamingResponseCache

### **Tidal-Specific Components (In Plugin)**
- **TidalStreamManifest**: DASH manifest parser for chunk URLs (Tidal-specific XML/MPD format)
- **TidalChunkDownloader**: Sequential chunk download and assembly (Tidal's streaming protocol)
- **TidalAudioFormatHandler**: M4A container with FLAC codec extraction (Tidal's format)
- **TidalQualityMapper**: Maps Lidarr quality to Tidal's AudioQuality enum
- **TidalConcurrentDownloadManager**: Semaphore-controlled album downloads

### **Shared Library Components (In Lidarr.Plugin.Common)**
- **BaseStreamingIndexer/DownloadClient**: Common streaming service patterns
- **StreamingApiRequestBuilder**: HTTP client with OAuth, rate limiting, retries
- **StreamingResponseCache**: Generic caching with TTL and memory management
- **OAuth2PKCEAuthenticationService**: Standard OAuth 2.0 + PKCE flow
- **StreamingModels**: Common models (StreamingTrack, StreamingAlbum, etc.)

### **CLI Architecture (Uses Plugin)**
- **CLI commands invoke plugin methods directly**
- **No business logic in CLI - pure interface layer**
- **CLI focuses on user interaction, plugin handles all streaming logic**

## Development Workflow

### **Plugin-First Development**

```bash
# 1. Clone with submodules
git clone --recursive <repo-url>

# 2. Build plugin first (core functionality)
dotnet build src/Tidalarr/

# 3. Build CLI (thin wrapper using plugin)
dotnet build TidalCLI/ -p:IncludeCLIFramework=true

# 4. Test through CLI (CLI uses plugin methods)
cd TidalCLI
dotnet run -- search "Miles Davis Kind of Blue"
dotnet run -- download-album <album-id>
```

### **Architecture Principle**
- **Plugin**: Contains all business logic, streaming protocols, format handling
- **CLI**: Thin interface layer that calls plugin methods
- **Shared Library**: Common patterns used by multiple streaming services

### **Production Deployment**

```bash
# 1. Clean production build
dotnet build --configuration Release

# 2. Deploy plugin DLL (no CLI dependencies)
cp bin/Release/net8.0/Lidarr.Plugin.Tidalarr.dll /path/to/lidarr/plugins/
```

## Shared Library Integration

Tidalarr integrates with `Lidarr.Plugin.Common` v1.1.0+ for:

- **60-70% code reduction** through shared utilities
- **Standardized authentication** (OAuth 2.0 + PKCE)
- **Unified caching and rate limiting**
- **Common HTTP client patterns**
- **Shared model mapping utilities**

### **Integration Status**

- ✅ **Phase 1**: Critical Infrastructure (inheritance patterns)
- ✅ **Phase 2**: Model Alignment (TidalModelMapper, caching)  
- ✅ **Phase 3**: Basic Service Integration (HTTP, auth)
- ⚠️ **Phase 4-6**: Advanced features (pending model property alignment)

## Configuration

### **Plugin Configuration**
- Configured through Lidarr UI: Settings → Indexers → Add → Tidalarr
- Settings handled by `TidalSettings` extending `BaseStreamingSettings`
- OAuth authentication managed by `TidalOAuthService`

### **OAuth Authorization URL Field (Do Not Remove)**

Tidalarr intentionally exposes an `OAuth Authorization URL` field in both the indexer and download client settings:

- **Location**: `src/Tidalarr/Integration/LidarrNative/TidalLidarrIndexerSettings.cs` and `TidalLidarrDownloadClientSettings.cs`
- **Property**: `OAuthAuthUrl` with `[FieldDefinition(0, ...)]`

**Why it exists**:
- Reduces OAuth setup friction and support/debug time
- Lidarr's UI does not reliably live-update computed fields inside the settings modal after `Test()`. This field exists so users can copy the auth URL without digging through logs, and so we have a reliable “plugin is loaded” signal in `/api/v1/*/schema`.
- The value is derived from `${ConfigPath}/pkce_state.json`. If missing/expired, the getter creates a fresh PKCE state file and returns the new URL (safe for schema rendering: best-effort, no throws).
- The field is intentionally derived/read-only (setter is a no-op)

**Regression history** (DO NOT REPEAT):
- ❌ Removed in `ff0cf39` ("remove non-functional OAuthAuthUrl field")
- ✅ Restored in `2b4225c` ("restore OAuthAuthUrl field with file-based implementation")

**When the field appears empty**:
- The `ConfigPath` is not set or is invalid
- You changed `ConfigPath` but haven’t saved/re-opened the modal yet (Lidarr typically evaluates computed fields when the modal is opened, not live while editing)
- Lidarr may not refresh this computed field inside the modal after clicking `Test()`. If you click Test and immediately need the URL, copy it from the validation error message, then refresh/re-open the settings modal to see the field populated.

**Redirect URL lifecycle (important)**:
- The OAuth Redirect URL is a one-time input used to exchange an auth code for tokens.
- Lidarr persists settings only when the user saves them; plugins cannot reliably mutate the stored Redirect URL value.
- If tokens expire and you see a state mismatch, the stored Redirect URL is stale. You do not need to clear it first; paste the NEW redirect URL from your most recent OAuth login (overwrite) and click Test again.

**When the field is missing entirely** (triage steps):
1. Confirm plugin is loaded: check `/api/v1/indexer/schema` for Tidalarr
2. Check Lidarr logs for plugin load errors
3. Multi-plugin runs can be affected by the upstream Lidarr AssemblyLoadContext lifecycle bug
4. Verify you're running the build with the field restored (`2b4225c` or later)

**Security**: `pkce_state.json` contains a PKCE `code_verifier`; never commit it or include it in logs/artifacts.

### **CLI Configuration**
```bash
# Configure authentication
dotnet run -- config set-auth --client-id your_id --client-secret your_secret

# Configure quality preferences
dotnet run -- config set-quality --preferred Lossless
```

## Testing

**IMPORTANT**: Always use the test runner script to ensure proper build flags:

```powershell
# Run all tests (recommended)
./scripts/test.ps1

# Run with filter
./scripts/test.ps1 -Filter "FullyQualifiedName~TidalApiClient"

# CI mode (excludes HostBridge tests)
./scripts/test.ps1 -ExcludeHostBridge
```

**Why not `dotnet test` directly?**
ILRepack merges dependencies with `Internalize=true`, making types like `IStreamingResponseCache` internal. Tests built without `-p:PluginPackagingDisable=true` will fail with `MissingMethodException`. The test script handles this automatically.

```bash
# Development build tests (with CLI framework)
dotnet test -p:IncludeCLIFramework=true -p:PluginPackagingDisable=true
```

## Troubleshooting

### **Build Issues**

**"System.CommandLine not found"**:
- Solution: Use `-p:IncludeCLIFramework=true` for development builds
- Root cause: CLI framework is opt-in for production-first architecture

**"Missing shared library dependencies"**:
- Solution: Update submodule: `git submodule update --remote`
- Check: `ext/Lidarr.Plugin.Common` is properly synced

### **CLI Issues**

**CLI commands not working**:
- Ensure CLI build: `dotnet build TidalCLI/ -p:IncludeCLIFramework=true`
- Verify CLI project references main plugin correctly

### **Multi-Plugin Co-Existence (FIXED 2026-05-10)**

Previously documented as "upstream Lidarr ALC lifecycle bug" — actually a plugin-side packaging issue. **Root-caused and fixed in common PR #485 + per-plugin host-version alignment.** See `ext/Lidarr.Plugin.Common/docs/dev-guide/ALC_MULTIPLUGIN_FIX.md` for the full retrospective.

**The rule**: every Tidalarr update must keep the merged plugin DLL free of `AssemblyRef`s the Lidarr host doesn't ship. Verify with:

```powershell
$pe = New-Object System.Reflection.PortableExecutable.PEReader([IO.MemoryStream]::new([IO.File]::ReadAllBytes('src/Tidalarr/bin/Lidarr.Plugin.Tidalarr.dll')))
$md = [System.Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($pe)
foreach ($arh in $md.AssemblyReferences) {
  $ar = $md.GetAssemblyReference($arh)
  Write-Host "$($md.GetString($ar.Name)) v$($ar.Version)"
}
```

**Host-pinned versions** (do NOT bump without verifying the host ships the same major version):

| Package | Pin | Lidarr host AssemblyVersion |
|---|---|---|
| `Microsoft.Extensions.DependencyInjection` | 8.0.1 | 8.0.0.0 |
| `Microsoft.Extensions.Logging` | 8.0.1 | 8.0.0.0 |
| `Microsoft.Extensions.Logging.Abstractions` | 8.0.3 | 8.0.0.0 |
| `Microsoft.Extensions.Http` | 8.0.1 | 8.0.0.0 |
| `FluentValidation` | 9.5.4 | 9.0.0.0 |
| `NLog` | 5.4.0 | 5.0.0.0 |

**Verify multi-plugin co-existence locally** when changing pins or packaging:

```powershell
pwsh ext/Lidarr.Plugin.Common/scripts/multi-plugin-coexistence-proof.ps1 -SkipBuild
```

(Spins up one Lidarr container with all locally-built plugins mounted; asserts each appears in `/api/v1/{indexer,downloadclient,importlist}/schema`.)

## Version Management

- Version managed in: `Tidalarr.csproj`
- Shared library version: Tracked via git submodule
- CLI framework: Conditionally included based on build flags

## Architecture Benefits

### **For Development Teams**
- ✅ Full CLI functionality with development flag
- ✅ Same convenience as before
- ✅ No workflow changes needed

### **For Production**
- ✅ Clean stable dependencies
- ✅ No pre-release packages
- ✅ Better deployment reliability
- ✅ Reduced attack surface

### **For External Adoption**
- ✅ Clean library experience by default
- ✅ No CLI baggage for plugin-only users
- ✅ Easier integration for other teams
- ✅ Future-proof scaling

## Contributing

1. **Development builds**: Always use `-p:IncludeCLIFramework=true`
2. **Test production builds**: Verify clean builds work with `-p:IncludeCLIFramework=false`
3. **Document changes**: Update this file if CLI dependencies change
4. **Follow shared library patterns**: Use `BaseStreamingIndexer`, `BaseStreamingDownloadClient`

---

## Quick Reference

```bash
# Development (default with CLI)
dotnet build -p:IncludeCLIFramework=true

# Production (clean dependencies)
dotnet build --configuration Release

# CLI testing
cd TidalCLI && dotnet run -- --help

# Shared library update
git submodule update --remote ext/Lidarr.Plugin.Common
```

This architecture ensures Tidalarr remains developer-friendly while providing production-ready, scalable deployments suitable for enterprise environments and external adoption.

---

## Technical Debt

This section tracks technical debt items that should be addressed but are not blocking current development. Technical debt is automatically prioritized and should never be put under the rug.

### Completed Items

| Item | Priority | Date | Description |
|------|----------|------|-------------|
| Quality Detection Enhancement | MEDIUM | 2025-01-25 | Fixed TidalSearchService to preserve API-detected qualities from audioQuality field; improved TidalApiClient.DetectAlbumQualities parsing |
| Artist ID Plumbing | LOW | 2024-12-XX | Added PrimaryArtistId to TidalTrackInfo and TidalAlbumInfo with fallback to name |

### Pending Items

| Item | Priority | File | Description |
|------|----------|------|-------------|
| None identified | - | - | Tidalarr has relatively clean architecture with good separation of concerns |

## Local Verification (Billing-Blocked CI)

When GitHub Actions billing is blocked, run the merge-critical verification pipeline locally:

```bash
pwsh scripts/verify-local.ps1                    # Full pipeline (extract + build + package + closure + E2E)
pwsh scripts/verify-local.ps1 -SkipExtract       # Fast rerun (reuse cached host assemblies)
pwsh scripts/verify-local.ps1 -SkipTests         # Build + packaging closure only
pwsh scripts/verify-local.ps1 -NoRestore         # Skip dotnet restore (fast iteration)
pwsh scripts/verify-local.ps1 -IncludeSmoke      # + Docker smoke test (mounts plugin in Lidarr)
```

**Prerequisites**: PowerShell 7+ (`pwsh`), .NET 8 SDK, Docker (for extract/smoke stages).

The script delegates to `ext/Lidarr.Plugin.Common/scripts/local-ci.ps1`, which orchestrates the same gates as CI: host assembly extraction with .NET 8 + FV 9.5.4 guardrails, plugin packaging via `New-PluginPackage`, and packaging closure validation via `generate-expected-contents.ps1 -Check`.

## Docker E2E Harness (wave 21)

A runnable end-to-end harness boots a real Lidarr container, mounts the merged
Tidalarr plugin DLL, waits for the API, and asserts plugin liveness against the
Lidarr REST API. This is the smoke alarm for "did the plugin actually load
inside the host?" — sandbox tests cannot answer that.

### Run locally

```powershell
# One-shot (builds plugin via verify-local.ps1, then runs the smoke matrix)
pwsh scripts/e2e.ps1

# Re-run without rebuilding (DLL already in src/Tidalarr/bin/)
pwsh scripts/e2e.ps1 -SkipBuild

# Run a single test
pwsh scripts/e2e.ps1 -Filter 'FullyQualifiedName~Indexer_Test'

# Or directly via dotnet (after building)
dotnet test tests/Tidalarr.Tests/Tidalarr.Tests.csproj -c Release \
    -p:PluginPackagingDisable=true --filter "Category=DockerE2E"
```

If Docker Desktop isn't running the tests **skip gracefully** rather than fail —
they're safe to leave in any local test command. CI wiring is out of scope until
wave 22.

### Pinned image

`ghcr.io/hotio/lidarr:pr-plugins-3.1.2.4913` (single-plugin instance on host
port `8690` per the multi-plugin guidance in this file). The tag is sourced
from `scripts/verify-local.ps1`'s `LidarrDockerVersion`. Bump in one place.

### What the smoke tests verify

All tests live in `tests/Tidalarr.Tests/Runtime/` and share one container via
`LidarrContainerFixture` (xUnit collection fixture, single startup per run):

| Test | Asserts |
|------|---------|
| `Plugin_Loads_AppearsInIndexerSchema` | `GET /api/v1/indexer/schema` lists Tidal |
| `Plugin_Loads_AppearsInDownloadClientSchema` | `GET /api/v1/downloadclient/schema` lists Tidal |
| `Indexer_Test_WithEmptySettings_ReturnsSensibleFailure` | `POST /api/v1/indexer/test` returns non-5xx (validation failure, not plugin-load failure) |
| `DownloadClient_Test_WithEmptySettings_ReturnsSensibleFailure` | `POST /api/v1/downloadclient/test` returns non-5xx |
| `Plugin_Loads_In_Real_Lidarr_Container` (`Category=Docker`, legacy) | wave-12 schema check, retained for backwards compat |

Acceptance criterion for the Test endpoints: **anything below 500**. A genuine
plugin-load failure (missing types, bad assemblies, ALC issues) shows up as a
500 InternalServerError. A 4xx with `[ { "errorMessage": "..." } ]` body is
expected — there's no real Tidal account.

### Adding a new smoke test

1. Add a new `[SkippableFact] [Trait("Category","DockerE2E")]` method to
   `DockerE2ETests.cs`, decorated with `[Collection(LidarrContainerCollection.Name)]`
   on the class so it shares the fixture.
2. Skip-guard with `Skip.If(_fixture.SkipReason is not null, _fixture.SkipReason);`
3. Use `_fixture.Http`, `_fixture.BaseUrl`, `_fixture.ApiKey` to talk to Lidarr.
   Call `_fixture.GetContainerLogs()` in failure messages so a CI rerun in
   another timezone can still tell you what blew up.

### Extending the harness to other plugins (wave 22a — done)

As of wave 22a the orchestrator (container lifecycle, healthcheck, log
capture, skip-when-no-Docker) lives in
`Lidarr.Plugin.Common.TestKit.Hosting.LidarrContainerFixture`. Each plugin
provides only the per-plugin glue:

- **`tests/<Plugin>.Tests/Runtime/LidarrContainerFixture.cs`** — subclass
  common's fixture and pass a `LidarrContainerOptions` record with the
  per-plugin knobs: `DockerImage`, `ContainerName`, `LidarrPort`,
  `PluginMountPath` (e.g. `/config/plugins/<Owner>/<PluginName>`),
  `PluginDllFileName`, a `FindPluginDll(repoRoot)` resolver, and a
  `PluginEntrySubstring` ("Tidal", "Qobuz", "AppleMusic", "Brainarr"). Define
  the xUnit `[CollectionDefinition]` next to it.
- **`tests/<Plugin>.Tests/Runtime/DockerE2ETests.cs`** — `[SkippableFact]`s
  that delegate to the smoke-assertion extension methods on the fixture
  (`AssertPluginAppearsInIndexerSchemaAsync`,
  `AssertPluginAppearsInDownloadClientSchemaAsync`,
  `AssertIndexerTestReturnsSensibleFailureAsync`,
  `AssertDownloadClientTestReturnsSensibleFailureAsync`).
- **`scripts/e2e.ps1`** — copy verbatim, adjust `verify-local.ps1` integration
  if that plugin's CI runner differs.

Wave 22b will use this to add Docker E2E to applemusicarr / qobuzarr /
brainarr — the per-plugin glue is ~30 lines.

## Flaky Tests Policy

**Flaky tests are priority tech debt that must be paid immediately.** A test that passes sometimes and fails sometimes erodes trust in the entire test suite. When a flaky test is discovered:

1. **Fix it before starting new feature work** — flaky tests block reliable CI
2. **Document the root cause** in a commit message so the pattern is not repeated
3. **Never skip or disable** a flaky test without a tracking issue

### Known Flaky Tests (Tidalarr)

| Test | Root Cause | Fix |
|------|-----------|-----|
| `HostVersionCouplingTests.DirectoryPackagesProps_Should_Match_HostVersions_For_Coupled_Dependencies` | Test reads FluentValidation.dll from `ext/Lidarr/_output` which may not exist in all dev environments (Docker-only assembly) | Guard with `Skip` when assembly directory is missing, or document required setup |
