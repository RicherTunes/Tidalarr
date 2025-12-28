# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Tidalarr is a high-performance Lidarr plugin for Tidal streaming service, built using the Lidarr.Plugin.Common shared library architecture. It provides both indexing and download capabilities for high-quality audio content from Tidal.

**ALWAYS**:
- Use constants from `TidalConstants.cs` rather than hardcoding.
- Expose to the user what brings value in `TidalDownloadSettings.cs` or `TidalarrSettings.cs`; otherwise, it should be in `TidalConstants.cs`.
- Be aware that this project shares a common library with http://github.com/RicherTunes/Lidarr.Plugin.Common so always think of ways to ensure generic code can be shared with this library so other projects may benefits. Think architecturally when doing so.

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

### **CLI Configuration**
```bash
# Configure authentication
dotnet run -- config set-auth --client-id your_id --client-secret your_secret

# Configure quality preferences
dotnet run -- config set-quality --preferred Lossless
```

## Testing

```bash
# Stable test runner (recommended)
pwsh ./scripts/test.ps1

# Stable runner with CLI-scope tests included
pwsh ./scripts/test.ps1 -IncludeCliTests

# Run all tests (may intermittently hit CS2012 file locks in the Common submodule build)
dotnet test

# Run specific test project
dotnet test tests/Tidalarr.Tests/

# Development build tests (with CLI)
dotnet test -p:IncludeCLIFramework=true
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
pwsh ./scripts/sync-ext-common-sha.ps1  # Sync SHA tracking file
```

This architecture ensures Tidalarr remains developer-friendly while providing production-ready, scalable deployments suitable for enterprise environments and external adoption.
