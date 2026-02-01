# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Tidalarr is a high-performance Lidarr plugin for Tidal streaming service, built using the Lidarr.Plugin.Common shared library architecture. It provides both indexing and download capabilities for high-quality audio content from Tidal.

**ALWAYS**:
- Use constants from `TidalConstants.cs` rather than hardcoding.
- Expose to the user what brings value in `TidalDownloadSettings.cs` or `TidalarrSettings.cs`; otherwise, it should be in `TidalConstants.cs`.
- Be aware that this project shares a common library with http://github.com/RicherTunes/Lidarr.Plugin.Common so always think of ways to ensure generic code can be shared with this library so other projects may benefits. Think architecturally when doing so.

## 🎯 Lidarr Runtime Requirements (Verified 2026-02-01)

| Branch | .NET Version | Docker Image | Lidarr Version |
|--------|--------------|--------------|----------------|
| **nightly** (plugins merged) | **.NET 8.0** | `ghcr.io/hotio/lidarr:nightly` | 3.1.2.4914+ |
| ~~pr-plugins-2.14.x~~ | ~~.NET 6.0~~ | ~~`ghcr.io/hotio/lidarr:pr-plugins-2.14.2.4786`~~ | ~~2.14.x~~ (OUTDATED) |

**CRITICAL**: As of Lidarr 3.1.x, the plugins/nightly branch runs on **.NET 8.0**. The plugin **MUST** target `net8.0`.

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
cp bin/Release/net6.0/Lidarr.Plugin.Tidalarr.dll /path/to/lidarr/plugins/
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

### **Multi-Plugin Testing**

**Intermittent failures on :8691 (multi-plugin instance)**:
- Root cause: Upstream Lidarr AssemblyLoadContext lifecycle bug
- Symptoms: Missing schemas after restart, type identity errors, non-deterministic test failures
- Workaround: Use dedicated single-plugin instance `:8690` for reliable Tidalarr E2E
- Status: `:8691` is "best-effort" until Lidarr ALC fix lands
- See: `ext/Lidarr.Plugin.Common/docs/ECOSYSTEM_PARITY_ROADMAP.md` for details

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
