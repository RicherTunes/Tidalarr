# Tidalarr

[![Production Ready](https://img.shields.io/badge/Status-Production%20Ready-brightgreen)](https://github.com/RicherTunes/Tidalarr/releases)
[![Version](https://img.shields.io/badge/version-1.0.1-brightgreen)](plugin.json)
[![.NET 8.0](https://img.shields.io/badge/.NET-8.0-purple)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Lidarr 3.0+](https://img.shields.io/badge/Lidarr-3.0%2B-orange)](https://lidarr.audio/)
[![License: GPL v3](https://img.shields.io/badge/License-GPL%20v3-blue.svg)](LICENSE)

## Overview

Tidalarr is a high-performance Lidarr plugin that indexes and downloads lossless audio directly from the Tidal streaming service. Built with a production-first architecture and sharing infrastructure through the Lidarr.Plugin.Common library, Tidalarr delivers seamless integration for automatic music search and acquisition.

[![Production Ready](https://img.shields.io/badge/Status-Production%20Ready-brightgreen)]()

**Current Version**: v1.0.1 | **Last Updated**: January 2025

---

## Features

### Core Functionality
- **High-Fidelity Audio**: Lossless FLAC and Hi-Res quality downloads (up to 24-bit/192kHz)
- **OAuth 2.0 PKCE Authentication**: Secure, standards-based authentication flow with automatic token refresh
- **Album & Track Downloads**: Full album downloads with automatic track sequencing
- **Smart Search Integration**: Direct integration with Lidarr's search and import workflow
- **Duplicate Prevention**: Intelligent detection to prevent re-downloading existing content

### Advanced Features
- **DASH Manifest Streaming**: Tidal's chunked download protocol with parallel download support
- **Concurrent Download Management**: Configurable parallel track and chunk downloads for optimal performance
- **Quality Selection**: Support for Low, High, Lossless, and Hi-Res quality tiers
- **Performance Tuning**: Configurable chunk delays and concurrency limits for rate limit management
- **Response Caching**: Intelligent caching reduces API calls and improves response times

### Enterprise/Production Features
- **Shared Library Architecture**: Built on Lidarr.Plugin.Common for 60-70% code reduction
- **Production-First Design**: Clean dependencies without pre-release CLI packages
- **Extensible CLI Framework**: Optional CLI tools for development and testing
- **Comprehensive Testing**: Unit tests with high coverage for critical components

---

## Installation

### Prerequisites

#### Requirements
- **Lidarr**: v3.0.0.4855 or higher (plugins or nightly branch)
- **.NET Runtime**: .NET 8.0
- **Service Account**: Tidal subscription (Hi-Fi or Family recommended for lossless quality)
- **OAuth Client**: Tidal API client credentials (application registration)

#### Platform Support
- **Docker**: `/config/plugins/RicherTunes/Tidalarr/`
- **Linux**: `/var/lib/lidarr/plugins/RicherTunes/Tidalarr/`
- **Windows**: `%ProgramData%\Lidarr\plugins\RicherTunes\Tidalarr\`
- **macOS**: `~/Library/Application Support/Lidarr/plugins/RicherTunes/Tidalarr/`

### Method 1: Install via Lidarr UI (Recommended)

**Best for**: Most users, quick installation, automatic updates

1. Ensure Lidarr is on the `plugins` or `nightly` branch at version `3.0.0.4855` or newer
   - Go to **Settings > General > Updates**
   - Set **Branch** to `plugins` or `nightly`
   - Update if needed and restart Lidarr
2. Go to **Settings > Plugins**
3. Click **Add Plugin**
4. Paste the repository URL: `https://github.com/RicherTunes/Tidalarr.git`
5. Click **Install**, then **Restart** when prompted
6. Configure the plugin in **Settings > Indexers** and **Settings > Download Clients**

**Notes:**
- Requires Lidarr plugins/nightly branch with plugin support
- Automatically handles dependencies and updates
- Recommended for most users

### Method 2: Installing from Releases

**Best for**: Manual control, specific versions, offline installation

1. **Download the latest release**:
   ```bash
   wget https://github.com/RicherTunes/Tidalarr/releases/latest/download/Tidalarr.zip
   ```

   Or download manually from [GitHub Releases](https://github.com/RicherTunes/Tidalarr/releases)

2. **Extract to Lidarr plugins directory**:
   ```bash
   # Create directory if needed
   mkdir -p /path/to/lidarr/plugins/RicherTunes/Tidalarr/

   # Extract
   unzip Tidalarr.zip -d /path/to/lidarr/plugins/RicherTunes/Tidalarr/
   ```

   **Platform-specific paths**: See [Ecosystem Installation Paths](https://github.com/RicherTunes/.github/blob/main/docs/ecosystem/PLATFORM_PATHS.md)

3. **Restart Lidarr**:
   ```bash
   # Docker
   docker restart lidarr

   # Linux
   systemctl restart lidarr

   # Windows: Restart Lidarr service
   # macOS: Restart Lidarr application
   ```

4. **Configure in Lidarr**:
   - Go to **Settings > Indexers** and add Tidalarr
   - Go to **Settings > Download Clients** and add Tidalarr

### Method 3: CLI Installation (Optional)

**Best for**: Developers, testing, standalone use

The CLI provides direct access for testing and standalone use:

```bash
cd TidalCLI
dotnet build -c Release -p:IncludeCLIFramework=true
dotnet run -- help
```

**Note**: CLI is optional - the plugin works fully integrated with Lidarr without it.

### Verification

After installation, verify:
- [ ] Plugin appears in Lidarr's Installed Plugins list
- [ ] Plugin files are present: `plugin.json`, `manifest.json`, `Lidarr.Plugin.Tidalarr.dll`
- [ ] Plugin is enabled and functional in **Settings > Indexers** and **Settings > Download Clients**
- [ ] No errors in **System > Logs** related to the plugin

---

## Configuration

### Plugin Configuration

Configure via Lidarr UI: **Settings → Indexers → Add → Tidalarr** and **Settings → Download Clients → Add → Tidalarr**

#### Required Settings (Indexer)
- **OAuth Authorization URL**: Click **Test** to generate the authorization URL, then authenticate with Tidal and paste the redirect URL back into the **OAuth Redirect URL** field
- **OAuth Redirect URL**: Paste the full redirect URL from your OAuth callback (e.g., `http://localhost:59027/callback/?code=...&state=...`)

#### Optional Settings (Indexer)
- **API Base URL**: Tidal API endpoint (default: `https://api.tidal.com`)
- **Request Delay (ms)**: Delay between API requests (default: `100`)
- **Enable Logging**: Enable detailed API logging for troubleshooting (default: `false`)

#### Required Settings (Download Client)
- **Quality**: Preferred audio quality - `Low`, `High`, `Lossless`, or `HiRes` (default: `Lossless`)
- **Chunk Delay (ms)**: Delay between chunk requests (default: `0`, set higher if rate limited)
- **Max Concurrent Track Downloads**: Parallel tracks per album (default: `2`, range: `1-3`)
- **Max Concurrent Chunk Downloads**: Parallel chunk requests per track (default: `2`, range: `1-8`)

### Advanced Configuration

#### Performance Tuning

Tidal downloads are chunked (many HTTP requests per track), so they will not match single-file providers 1:1. The defaults aim for a safe baseline; raise cautiously if you hit slow downloads.

- **Chunk Delay (ms)**: Use `0` for maximum speed; increase if you get rate limited. Note: When > 0, chunk parallelism is disabled to preserve "delay between requests" semantics.
- **Max Concurrent Track Downloads**: Controls album-level parallelism. Higher values download faster but use more connections.
- **Max Concurrent Chunk Downloads**: Controls track-level parallelism. Only effective when Chunk Delay is 0.

#### OAuth Authentication Notes

The **OAuth Authorization URL** field is intentionally exposed to reduce setup friction:

- The URL is automatically generated from `${ConfigPath}/pkce_state.json`
- If the field appears empty, click **Test** to generate a fresh state
- The redirect URL from OAuth callback is a one-time input - paste the full URL and click **Test** again
- If tokens expire, paste the NEW redirect URL (overwrite the old one) - no need to clear first

**Security**: `pkce_state.json` contains a PKCE `code_verifier`; never commit it or include it in logs/artifacts.

**For detailed configuration**, see [Configuration Guide](docs/CONFIGURATION.md) if available.

---

## Usage

### Plugin Usage

Tidalarr integrates with Lidarr's standard indexer and download client workflow:

**Typical Workflow:**
1. **Configure**: Set up OAuth authentication by clicking **Test** and completing the OAuth flow
2. **Add Indexer**: Add Tidalarr as an indexer in **Settings > Indexers**
3. **Add Download Client**: Add Tidalarr as a download client in **Settings > Download Clients**
4. **Search**: Use Lidarr's standard search (manual or automatic) to find albums and tracks
5. **Download**: Tidalarr automatically handles chunked downloads and quality selection

**Integration Points:**
- **Album Search**: Tidalarr indexes albums by artist, album title, and UPC
- **Track Search**: Individual track search and download support
- **Quality Selection**: Automatic quality selection based on preferences
- **Import Integration**: Seamless integration with Lidarr's import workflow

### CLI Usage (Optional)

The CLI tool is useful for development, testing, and standalone use:

```bash
# Search for music
dotnet run -- search "Miles Davis Kind of Blue"

# Download a track
dotnet run -- download-track <trackId> /output/dir --quality Lossless

# Download an album
dotnet run -- download-album <albumId> /output/dir --quality HiRes

# Named arguments (also supported)
dotnet run -- download-album AlbumId=<id> OutputDir=/output/dir Quality=Lossless
```

#### Common CLI Commands

| Command | Description | Example |
|---------|-------------|---------|
| `search <query>` | Search for albums and tracks | `dotnet run -- search "Artist Album"` |
| `download-track <id> <dir>` | Download a single track | `dotnet run -- download-track 12345 ./music` |
| `download-album <id> <dir>` | Download an entire album | `dotnet run -- download-album 67890 ./music` |
| `config` | Manage CLI configuration | `dotnet run -- config set-quality Lossless` |

**Note**: CLI is optional - the plugin works fully integrated with Lidarr without it.

---

## Architecture

### Design Philosophy

Tidalarr follows a **production-first, plugin-first** architecture:

- **Plugin-First**: All business logic resides in the plugin; CLI is a thin wrapper
- **Shared Library**: 60-70% code reduction through Lidarr.Plugin.Common integration
- **Production-Ready**: Clean dependencies without pre-release packages by default
- **Developer-Friendly**: CLI tools available with build flag for development

### Project Structure

```
src/
├── Tidalarr/                    # Core plugin (Lidarr.Plugin.Tidalarr.dll)
│   ├── Core/                    # Core models, interfaces, constants
│   ├── Domain/                  # API clients, authentication, streaming
│   │   ├── Auth/               # OAuth 2.0 + PKCE implementation
│   │   ├── Streaming/          # DASH manifest parsing, chunk downloads
│   │   └── API/                # Tidal API client
│   ├── Infrastructure/          # Caching, performance, storage
│   ├── Integration/             # Lidarr integration (indexer, download client)
│   └── Application/             # Application services
│
├── Tidalarr.HostBridge/         # Host-only wrappers (NzbDrone annotations)
│   └── Settings/                # UI settings mapping to core
│
TidalCLI/                         # CLI wrapper for testing and development
├── Commands/                     # CLI command implementations
├── Services/                     # CLI-specific service adapters
└── Program.cs                    # CLI entry point

ext/Lidarr.Plugin.Common/         # Shared library (submodule v1.5.0)
```

**Key Components**:
- **TidalIndexer**: Implements Lidarr's indexer interface for search
- **TidalDownloadClient**: Implements Lidarr's download client interface
- **TidalStreamManifest**: DASH manifest parser for Tidal's streaming protocol
- **TidalChunkDownloader**: Sequential chunk download and assembly
- **TidalAudioFormatHandler**: M4A container with FLAC codec extraction
- **TidalOAuthService**: OAuth 2.0 + PKCE authentication flow
- **TidalModelMapper**: Maps Tidal models to shared library models

### Design Principles
- **Separation of Concerns**: Core plugin is hostless; host bridge handles UI integration
- **Shared Infrastructure**: Common patterns moved to Lidarr.Plugin.Common
- **Conditional Compilation**: CLI framework only included when needed
- **Testability**: All components designed for unit testing with high coverage

### Technology Stack

- **Platform**: .NET 8.0 (Lidarr plugin framework)
- **Authentication**: OAuth 2.0 + PKCE (RFC 7636)
- **Streaming Protocol**: DASH (Dynamic Adaptive Streaming over HTTP)
- **Audio Format**: M4A container with FLAC codec
- **HTTP Client**: HttpClient with StreamingApiRequestBuilder pattern
- **Caching**: StreamingResponseCache with TTL and memory management
- **Testing**: xUnit, Moq, FluentAssertions

### Shared Library Integration

This plugin integrates with [Lidarr.Plugin.Common](https://github.com/RicherTunes/Lidarr.Plugin.Common) v1.5.0 for:

- **Authentication**: OAuth 2.0 + PKCE base class (80% code reduction)
- **HTTP Client**: StreamingApiRequestBuilder with retries and rate limiting
- **Caching**: StreamingResponseCache with intelligent memory management
- **Models**: Common streaming models (StreamingTrack, StreamingAlbum, etc.)
- **Base Classes**: BaseStreamingIndexer and BaseStreamingDownloadClient

**Shared library advantages:**
- Code reuse across multiple streaming service plugins
- Consistent patterns and interfaces
- Reduced maintenance burden
- Community improvements benefit all plugins

**For detailed architecture**, see [Architecture Documentation](docs/ARCHITECTURE.md) if available.

---

## Performance

### Optimization Results
- **60-70% code reduction** through shared library integration ✅
- **Intelligent caching** reduces redundant API calls ✅
- **Parallel chunk downloads** for improved throughput ✅

**Performance Notes:**
- Tidal downloads are chunked (many HTTP requests per track), so they will not match single-file providers 1:1
- Defaults are conservative for reliability; tune based on your network conditions
- Chunk delays help avoid rate limiting while maintaining good performance

### Resource Usage
- **Memory**: ~200MB baseline, ~400MB during concurrent album downloads
- **CPU**: Minimal usage with async/await patterns
- **Network**: Multiple concurrent connections for chunked downloads
- **Disk I/O**: Sequential writes during chunk assembly

### Performance Tuning

For optimal performance:

1. **Start with defaults** - They're tuned for reliability
2. **Monitor logs** - Check for rate limit errors
3. **Adjust chunk delay** - Increase if rate limited, decrease for speed
4. **Tune concurrency** - Higher values for faster networks, lower for slower connections
5. **Use Lossless quality** - HiRes requires more bandwidth and storage

---

## Troubleshooting

### Common Issues

#### Issue: OAuth authentication fails
- **Symptoms**: "Invalid state" error, token refresh fails, authorization URL not generated
- **Solution**:
  1. Click **Test** to generate a fresh OAuth Authorization URL
  2. Complete the OAuth flow in your browser
  3. Copy the ENTIRE redirect URL (including `code=` and `state=` parameters)
  4. Paste into **OAuth Redirect URL** field
  5. Click **Test** again to exchange code for tokens
  6. If tokens expired, paste the NEW redirect URL (overwrite, don't clear)
- **Prevention**: Keep OAuth tokens refreshed; redirect URL is one-time use

#### Issue: Slow downloads or rate limiting
- **Symptoms**: Downloads stall, 429 errors, slow chunk downloads
- **Solution**:
  1. Increase **Chunk Delay (ms)** to 100-200ms
  2. Reduce **Max Concurrent Chunk Downloads** to 1-2
  3. Reduce **Max Concurrent Track Downloads** to 1
- **Prevention**: Use conservative defaults for always-on systems

#### Issue: Plugin not loading in Lidarr
- **Symptoms**: Plugin doesn't appear in Settings, missing schemas
- **Solution**:
  1. Verify Lidarr version is 3.0.0.4855 or higher
  2. Check plugin files are in correct directory
  3. Restart Lidarr completely
  4. Check logs for assembly load errors
- **Prevention**: Always use recommended Lidarr version

#### Issue: OAuth Authorization URL field is empty
- **Symptoms**: The OAuth Authorization URL field appears blank in settings
- **Solution**:
  1. Verify plugin is loaded (check `/api/v1/indexer/schema` for Tidalarr)
  2. Click **Test** to generate a fresh PKCE state and URL
  3. If still empty, check Lidarr logs for plugin load errors
  4. Ensure ConfigPath is correctly set by saving settings first
- **Prevention**: This field is computed on modal open; refresh settings modal if needed

### Debug Logging

Enable debug logging in Lidarr:
1. Go to **Settings > General**
2. Set **Log Level** to **Debug**
3. Restart Lidarr
4. Reproduce the issue
5. Check **System > Logs** for detailed output

### Getting Help

- **Documentation**: See [docs/](docs/) for detailed guides
- **Issues**: [GitHub Issues](https://github.com/RicherTunes/Tidalarr/issues) - Bug reports and feature requests
- **Discussions**: [GitHub Discussions](https://github.com/RicherTunes/Tidalarr/discussions) - Questions and community support
- **Repository Guidelines**: See [AGENTS.md](AGENTS.md) for development guidelines

**Before asking for help:**
1. Check existing issues and discussions
2. Enable debug logging and gather logs
3. Include your Lidarr version and plugin version
4. Provide steps to reproduce the issue
5. Share relevant log excerpts (sanitize credentials first)

---

## Documentation

- [Configuration Guide](docs/CONFIGURATION.md) - Detailed setup instructions
- [Architecture](docs/ARCHITECTURE.md) - System design details
- [Host Bridge Integration](docs/hostbridge-integration.md) - Host-only settings wiring
- [TFM Rationale](docs/TFM_RATIONALE.md) - Target framework choices
- [AGENTS.md](AGENTS.md) - Repository guidelines for coding and testing
- [CLAUDE.md](CLAUDE.md) - Guidance for Claude Code and automation agents

**Additional Resources:**
- [Lidarr Documentation](https://wiki.lidarr.audio/)
- [Lidarr Plugin System](https://lidarr.audio/docs/plugins)
- [Lidarr.Plugin.Common](https://github.com/RicherTunes/Lidarr.Plugin.Common) - Shared library documentation

---

## Development

### Development Setup

```bash
# Clone the repository
git clone https://github.com/RicherTunes/Tidalarr.git
cd Tidalarr

# Initialize submodules
git submodule update --init --recursive

# Restore dependencies
dotnet restore Tidalarr.sln

# Build the solution
dotnet build Tidalarr.sln

# Run tests
./scripts/test.ps1
```

### Development Commands

```bash
# Build (production - clean dependencies)
dotnet build --configuration Release

# Build (development - includes CLI framework)
dotnet build -p:IncludeCLIFramework=true

# Run all tests using test script (recommended)
./scripts/test.ps1

# Run tests with filter
./scripts/test.ps1 -Filter "FullyQualifiedName~TidalApiClient"

# Run specific test categories
dotnet test --filter Category=Integration
dotnet test --filter Category=Unit
```

**Why use the test script?**
ILRepack merges dependencies with `Internalize=true`, making types internal. Tests built without `-p:PluginPackagingDisable=true` will fail. The test script handles this automatically.

### Project Structure

```
src/                          # Main plugin source
├── Tidalarr/                 # Core plugin implementation
│   ├── Core/                 # Core models, interfaces, constants
│   ├── Domain/               # API clients, authentication, streaming
│   ├── Infrastructure/       # Caching, performance, storage
│   ├── Integration/          # Lidarr integration
│   └── Application/          # Application services
├── Tidalarr.HostBridge/      # Host-only wrappers (not shipped)
TidalCLI/                      # CLI tool (optional)
├── Commands/                 # CLI command implementations
├── Services/                 # CLI-specific adapters
└── Program.cs                # CLI entry point
ext/                          # External dependencies
├── Lidarr.Plugin.Common/     # Shared library (submodule v1.5.0)
├── Lidarr/                   # Lidarr assemblies (submodule)
docs/                         # Documentation
scripts/                      # Build and test scripts
tests/                        # Test projects
```

### Contributing

We welcome contributions! Please see [AGENTS.md](AGENTS.md) for coding, testing, and review expectations.

**Key Points:**
- Follow existing code patterns and conventions
- Use constants from `TidalConstants.cs` rather than hardcoding
- Add tests for new features with high coverage
- Update documentation as needed
- Submit PRs with clear descriptions
- Ensure all tests pass before submitting

**Architecture Guidelines:**
- Think architecturally - can code be shared with Lidarr.Plugin.Common?
- Expose to users what brings value in settings; otherwise use constants
- Follow plugin-first design - CLI is a thin wrapper

---

## Security

### Security Posture

- **No hardcoded credentials** - All credentials stored securely via Lidarr's configuration system
- **Input validation** - All user inputs are validated and sanitized
- **Rate limiting** - Prevents API abuse and respects Tidal's service limits
- **Secure token storage** - OAuth tokens encrypted at rest in Lidarr's database
- **PKCE authentication** - Uses RFC 7636 Proof Key for Code Exchange for enhanced security

### Data Handling

- **Credentials**: Stored in Lidarr's secure configuration database
- **OAuth State**: PKCE code_verifier stored in `${ConfigPath}/pkce_state.json` (never committed)
- **Data Privacy**: No telemetry or usage data collected
- **Encryption**: OAuth tokens encrypted by Lidarr
- **Logging**: No sensitive data logged (credentials, tokens, code_verifiers)

### Vulnerability Reporting

See [SECURITY.md](SECURITY.md) for:
- Security policy
- Vulnerability reporting guidelines
- Security best practices
- Supported versions

---

## Related Plugins

This plugin is part of the RicherTunes plugin ecosystem:

- **[Brainarr](https://github.com/RicherTunes/brainarr)** - AI-powered music recommendations
- **[Qobuzarr](https://github.com/RicherTunes/qobuzarr)** - Qobuz streaming with ML optimization
- **[AppleMusicarr](https://github.com/RicherTunes/AppleMusicarr)** - Apple Music library sync

**Shared foundation**: [Lidarr.Plugin.Common](https://github.com/RicherTunes/Lidarr.Plugin.Common)

---

## Credits

### Original Authors
- **[RicherTunes](https://github.com/RicherTunes)** - Tidalarr implementation and architecture

### Core Contributors
- **Lidarr Team** - For the excellent media management platform and plugin framework
- **Tidal** - For providing the streaming service and high-quality audio catalog

### Ecosystem
This plugin is part of the RicherTunes Lidarr plugin ecosystem, sharing infrastructure through [Lidarr.Plugin.Common](https://github.com/RicherTunes/Lidarr.Plugin.Common).

See [CREDITS.md](CREDITS.md) for full list of contributors.

---

## Support

- **Issues**: [GitHub Issues](https://github.com/RicherTunes/Tidalarr/issues) - Bug reports and feature requests
- **Discussions**: [GitHub Discussions](https://github.com/RicherTunes/Tidalarr/discussions) - Questions and community support
- **Repository Guidelines**: [AGENTS.md](AGENTS.md) - Development and contribution guidelines

**Getting Help:**
1. Check existing issues and discussions
2. Read the documentation in [docs/](docs/)
3. Enable debug logging and gather information
4. Create a new issue with details (Lidarr version, plugin version, steps to reproduce, logs)

## License

This project is licensed under the GNU General Public License v3.0 - see the [LICENSE](LICENSE) file for details.

### License Summary

- ✅ Commercial use allowed
- ✅ Modification allowed
- ✅ Distribution allowed
- ✅ Private use allowed
- ⚠️ Liability and warranty disclaimed
- ❌ Requires same license for derivatives
- ❌ Requires source code disclosure for derivatives

## Disclaimer

This plugin is not affiliated with or endorsed by Tidal. Use of this plugin requires:
- A valid Tidal subscription
- Compliance with Tidal's Terms of Service
- Respect for copyright and intellectual property laws

**Important**: This plugin is a tool for managing your legally-acquired music collection. Users are responsible for ensuring their use complies with applicable laws and service terms.

---

**Current Version**: v1.0.1 | **Last Updated**: January 2025

---
