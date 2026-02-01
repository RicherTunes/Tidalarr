# Tidalarr - Development Guide

Build instructions, testing, and project structure for Tidalarr developers.

---

## Overview

This guide covers the development workflow, build process, testing strategy, and project structure for Tidalarr. It's designed for contributors who want to build, test, and extend the plugin.

---

## Development Environment Setup

### Prerequisites

- **.NET 8.0 SDK**: [Download](https://dotnet.microsoft.com/download/dotnet/8.0)
- **Git**: For version control
- **Visual Studio 2022** or **VS Code** (optional)
- **PowerShell**: For test scripts

### Repository Setup

```bash
# Clone the repository
git clone https://github.com/RicherTunes/Tidalarr.git
cd Tidalarr

# Initialize submodules
git submodule update --init --recursive

# Verify submodule status
git submodule status
```

### Build Dependencies

Tidalarr depends on:
- **Lidarr.Plugin.Common v1.5.0**: Shared library (submodule)
- **Lidarr assemblies**: For integration testing (submodule)

---

## Build Commands

### Development Builds (with CLI tools)

For development work that requires CLI framework dependencies:

```bash
# Development build with CLI framework
dotnet build -p:IncludeCLIFramework=true

# Development build with specific configuration
dotnet build --configuration Debug -p:IncludeCLIFramework=true

# Restore and build for development
dotnet restore && dotnet build -p:IncludeCLIFramework=true
```

### Production Builds (clean dependencies)

For production deployments without pre-release CLI dependencies:

```bash
# Production build (default - clean dependencies)
dotnet build

# Production release build
dotnet build --configuration Release

# Production build with explicit CLI exclusion
dotnet build -p:IncludeCLIFramework=false
```

### Building Specific Projects

```bash
# Build only core plugin
dotnet build src/Tidalarr/

# Build only CLI
dotnet build TidalCLI/ -p:IncludeCLIFramework=true

# Build test projects
dotnet build tests/
```

---

## Testing

### Important: Use the Test Script

**ALWAYS** use the test runner script instead of `dotnet test` directly:

```powershell
# Run all tests (recommended)
./scripts/test.ps1

# Run with filter
./scripts/test.ps1 -Filter "FullyQualifiedName~TidalApiClient"

# Run with exclusion
./scripts/test.ps1 -ExcludeHostBridge

# Run specific test categories
./scripts/test.ps1 -Filter "Category=Integration"
./scripts/test.ps1 -Filter "Category=Unit"
```

### Why Not `dotnet test` Directly?

ILRepack merges dependencies with `Internalize=true`, making types like `IStreamingResponseCache` internal. Tests built without proper flags will fail with `MissingMethodException`. The test script handles this automatically.

### Testing Categories

| Category | Description | Command |
|----------|-------------|---------|
| **Unit** | Core functionality tests | `./scripts/test.ps1 -Filter "Category=Unit"` |
| **Integration** | API and authentication tests | `./scripts/test.ps1 -Filter "Category=Integration"` |
| **HostBridge** | UI integration tests (excluded by default) | `./scripts/test.ps1 -Filter "Category=HostBridge"` |

### Test Coverage

Target coverage areas:
- **OAuth Authentication**: Flow, token refresh, error handling
- **API Client**: HTTP requests, retries, rate limiting
- **Streaming**: DASH parsing, chunk downloads
- **Caching**: Response caching and invalidation
- **Integration**: End-to-end workflows
- **CLI**: Command interface validation

---

## Project Structure

```
Tidalarr/
├── src/                        # Main source code
│   ├── Tidalarr/              # Core plugin (Lidarr.Plugin.Tidalarr.dll)
│   │   ├── Core/              # Core models, interfaces, constants
│   │   │   ├── Models/        # Tidal-specific data models
│   │   │   ├── Interfaces/    # Plugin interfaces
│   │   │   └── Constants.cs  # Configuration constants
│   │   ├── Domain/            # API clients, authentication, streaming
│   │   │   ├── Auth/          # OAuth 2.0 + PKCE implementation
│   │   │   ├── Streaming/     # DASH manifest processing, chunk downloads
│   │   │   └── API/           # Tidal API client
│   │   ├── Infrastructure/   # Caching, performance, storage
│   │   ├── Integration/       # Lidarr integration points
│   │   └── Application/       # Application services
│   └── Tidalarr.HostBridge/   # Host-only wrappers (not shipped)
│       └── Settings/         # UI settings with NzbDrone annotations
│
├── TidalCLI/                  # CLI wrapper (optional)
│   ├── Commands/              # CLI command implementations
│   ├── Services/              # CLI-specific service adapters
│   └── Program.cs             # CLI entry point
│
├── tests/                     # Test projects
│   ├── Tidalarr.Tests/        # Plugin unit and integration tests
│   └── TidalCLI.Tests/        # CLI command tests
│
├── ext/                       # External dependencies
│   ├── Lidarr.Plugin.Common/  # Shared library (submodule v1.5.0)
│   └── Lidarr/                # Lidarr assemblies (submodule)
│
├── scripts/                   # Build and test scripts
├── docs/                      # Documentation
└── wiki-content/              # User documentation (for GitHub wiki)
```

### Key Components

#### Core Plugin (Tidalarr.dll)
- **TidalIndexer**: Implements Lidarr's indexer interface
- **TidalDownloadClient**: Implements Lidarr's download client interface
- **TidalApiClient**: HTTP client using StreamingApiRequestBuilder
- **TidalOAuthService**: OAuth 2.0 + PKCE authentication
- **TidalStreamManifest**: DASH manifest parser
- **TidalChunkDownloader**: Chunk download and assembly
- **TidalModelMapper**: Model conversion utilities

#### HostBridge (Tidalarr.HostBridge.dll)
- **TidalIndexerHostSettings**: UI settings for indexer
- **TidalDownloadClientHostSettings**: UI settings for download client
- **SettingsMapper**: Converts host to core settings

#### CLI (TidalCLI.dll)
- **SearchCommand**: Search Tidal's catalog
- **DownloadCommand**: Download tracks/albums
- **ConfigCommand**: Manage CLI configuration

---

## Architecture Principles

### Plugin-First Design

- **Core Logic**: All business logic in main plugin assembly
- **CLI Wrapper**: Thin interface layer only
- **Host Bridge**: Separate UI integration assembly (not shipped)

### Shared Library Integration

Tidalarr uses Lidarr.Plugin.Common for:

- **Base Classes**: `BaseStreamingIndexer`, `BaseStreamingDownloadClient`
- **Authentication**: `OAuth2PKCEAuthenticationService`
- **HTTP Client**: `StreamingApiRequestBuilder` with retries/rate limiting
- **Caching**: `StreamingResponseCache` with TTL management
- **Models**: Common streaming models (`StreamingTrack`, `StreamingAlbum`)

### Component Responsibilities

| Component | Responsibility |
|-----------|----------------|
| **Core Plugin** | Business logic, API calls, streaming protocols |
| **Host Bridge** | UI forms, settings validation, user interaction |
| **CLI** | Command-line interface, user interaction |
| **Shared Library** | Common patterns, infrastructure utilities |

---

## Development Workflow

### Feature Development

1. **Create Branch**
   ```bash
   git checkout -b feature/new-feature
   ```

2. **Make Changes**
   - Work in appropriate directory (`src/Tidalarr/` for core logic)
   - Follow existing patterns and naming conventions
   - Use constants from `TidalConstants.cs`

3. **Write Tests**
   - Add tests for new functionality
   - Use `./scripts/test.ps1` to run tests
   - Aim for high test coverage

4. **Build and Test**
   ```bash
   # Development build
   dotnet build -p:IncludeCLIFramework=true

   # Test production build
   dotnet build --configuration Release
   ./scripts/test.ps1
   ```

5. **Commit Changes**
   ```bash
   git add .
   git commit -m "feat: add new feature"
   ```

6. **Create Pull Request**
   - Include detailed description
   - Link to relevant issues
   - Ensure all tests pass

### Testing Workflow

```bash
# 1. Development build with CLI
dotnet build -p:IncludeCLIFramework=true

# 2. Run all tests
./scripts/test.ps1

# 3. Run specific tests
./scripts/test.ps1 -Filter "FullyQualifiedName~TidalOAuthService"

# 4. Test production build
dotnet build --configuration Release
./scripts/test.ps1
```

### Multi-Plugin Testing

**Note**: Multi-plugin testing can be affected by Lidarr's AssemblyLoadContext lifecycle bug:

- **Issue**: Intermittent failures on :8691 (multi-plugin instance)
- **Symptoms**: Missing schemas, type identity errors
- **Workaround**: Use dedicated single-plugin instance :8690 for reliable testing
- **Status**: :8691 is "best-effort" until Lidarr fix

---

## Code Guidelines

### Coding Standards

- Follow existing code patterns and conventions
- Use constants from `TidalConstants.cs` (no magic numbers/strings)
- Prefer async/await for all I/O operations
- Use dependency injection through constructor injection
- Follow SOLID principles

### Testing Standards

- Write tests for all new features
- Aim for >80% test coverage
- Use FluentAssertions for assertions
- Mock external dependencies with Moq
- Test both success and error cases

### Naming Conventions

| Pattern | Example |
|---------|---------|
| **Classes**: PascalCase | `TidalApiClient` |
| **Methods**: PascalCase | `GetPlaybackInfoAsync` |
| **Properties**: PascalCase | `ApiBaseUrl` |
| **Fields**: camelCase | `_httpClient` |
| **Constants**: PascalCase | `TidalConstants.DefaultRequestDelay` |

### Documentation Standards

- Document all public APIs with XML comments
- Include examples for complex methods
- Document configuration settings
- Keep code comments concise and focused

---

## Debugging

### Debug Build

```bash
# Debug build with full symbols
dotnet build --configuration Debug -p:IncludeCLIFramework=true

# Debug with specific configuration
dotnet build --configuration Debug -p:IncludeCLIFramework=true -p:DebugSymbols=true
```

### Debugging with Visual Studio

1. Open `Tidalarr.sln` in Visual Studio
2. Set startup project to `Tidalarr`
3. Configure debug properties:
   - Launch: `Self-Host`
   - Working directory: Project directory
4. Set breakpoints and debug

### Debugging with VS Code

1. Open root directory in VS Code
2. Create `.vscode/launch.json`:
```json
{
  "version": "0.2.0",
  "configurations": [
    {
      "name": "Launch Tidalarr",
      "type": "coreclr",
      "request": "launch",
      "preLaunchTask": "build",
      "program": "${workspaceFolder}/src/Tidalarr/bin/Debug/net8.0/Tidalarr.dll",
      "args": [],
      "cwd": "${workspaceFolder}",
      "console": "integratedTerminal",
      "stopAtEntry": false
    }
  ]
}
```

### Debug Logging

Enable debug logging in development:

```csharp
// In your code
_logger.LogDebug("Debug message: {Message}", data);

// In settings
public bool EnableLogging { get; set; } = true;
```

---

## Release Process

### Version Management

- Version defined in `Tidalarr.csproj`
- Follow Semantic Versioning (SemVer)
- Update version before creating release

### Release Steps

1. **Update Version**
   ```xml
   <!-- In Tidalarr.csproj -->
   <Version>1.0.1</Version>
   ```

2. **Update Documentation**
   - Update version numbers in all docs
   - Update changelog if applicable

3. **Test Production Build**
   ```bash
   dotnet build --configuration Release
   ./scripts/test.ps1
   ```

4. **Create Release**
   - Tag release: `git tag v1.0.1`
   - Push: `git push origin v1.0.1`
   - Create GitHub release

5. **Update Submodule**
   - If Lidarr.Plugin.Common updated:
   ```bash
   git submodule update --remote
   git commit -am "deps: update Lidarr.Plugin.Common"
   ```

---

## Common Issues

### Build Issues

**"System.CommandLine not found"**:
- Solution: Use `-p:IncludeCLIFramework=true` for development builds
- Root cause: CLI framework is opt-in for production-first architecture

**"Missing shared library dependencies"**:
- Solution: Update submodule: `git submodule update --remote`
- Check: `ext/Lidarr.Plugin.Common` is properly synced

### Test Issues

**"MissingMethodException"**:
- Solution: Use `./scripts/test.ps1` instead of `dotnet test`
- Root cause: ILRepack makes types internal

**"Intermittent failures"**:
- Solution: Use single-plugin instance :8690 for reliable testing
- Root cause: Lidarr AssemblyLoadContext lifecycle bug

### Multi-Plugin Issues

**"Schema errors"**:
- Solution: Restart Lidarr completely between plugin changes
- Root cause: Plugin loading conflicts in multi-plugin environment

---

## Contributing

### Pull Request Guidelines

1. **Feature Branch**: Create from `main`
2. **Descriptive Title**: Use conventional commits format
3. **Detailed Description**: Explain changes and reasoning
4. **Testing**: All tests must pass
5. **Documentation**: Update docs for new features
6. **Review**: Address all review comments

### Commit Message Format

```
type(scope): description

[body]

footer
```

Types:
- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation
- `style`: Code style
- `refactor`: Refactoring
- `test`: Tests
- `chore`: Build/CI

### Code Review Checklist

- [ ] Code follows project conventions
- [ ] Tests cover new functionality
- [ ] Documentation is updated
- [ ] Performance considerations addressed
- [ ] Security implications considered
- [ ] Error handling comprehensive

---

## Performance Considerations

### Memory Usage

- Baseline: ~200MB
- During downloads: ~400MB
- Monitor with: `dotnet-counters monitor`

### CPU Usage

- Minimal with async/await patterns
- Monitor with: `dotnet-trace collect`

### Network Usage

- Multiple concurrent connections for chunked downloads
- Monitor with: `netstat -an | grep 8686`

---

## See Also

- [Architecture Documentation](../ARCHITECTURE.md) - System design details
- [Configuration Guide](../CONFIGURATION.md) - Complete configuration reference
- [Host Bridge Integration](../hostbridge-integration.md) - UI settings wiring
- [Lidarr.Plugin.Common](https://github.com/RicherTunes/Lidarr.Plugin.Common) - Shared library
- [AGENTS.md](../AGENTS.md) - Repository guidelines for coding and testing

---

**Current Version**: v1.0.1 | **Last Updated**: January 2025