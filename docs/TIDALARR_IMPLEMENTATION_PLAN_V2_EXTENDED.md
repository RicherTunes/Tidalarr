# Tidalarr Implementation Plan v2 - Extended
## Includes TidalCLI and Strategic Qobuzarr Refactoring

---

## Executive Summary

This extended plan adds TidalCLI as a test bed application following QobuzCLI's plugin-first pattern, and proposes strategic Qobuzarr refactoring that would benefit both projects.

---

## 1. Updated Project Structure

```
Tidalarr/
├── src/
│   ├── TidalCore/                   # Direct port of TidalSharp logic
│   │   ├── API.cs                   # Exact port from TidalSharp
│   │   ├── Session.cs               # Exact port from TidalSharp  
│   │   ├── Decryption.cs            # Exact port from TidalSharp
│   │   ├── Manifest.cs              # Exact port from TidalSharp
│   │   ├── Models/                  # All TidalSharp models
│   │   └── Globals.cs               # All hardcoded values from TidalSharp
│   │
│   ├── Integration/                 # Qobuzarr-style integration layer
│   │   ├── TidalIndexer.cs          # Implements HttpIndexerBase
│   │   ├── TidalDownloadClient.cs   # Implements DownloadClientBase
│   │   ├── TidalIndexerSettings.cs  # Lidarr settings UI
│   │   └── TidalDownloadSettings.cs # Lidarr settings UI
│   │
│   ├── Services/                    # Adapter layer
│   │   ├── TidalSessionAdapter.cs   # Wraps TidalSharp's Session
│   │   ├── TidalApiAdapter.cs       # Wraps TidalSharp's API
│   │   └── TidalDownloadAdapter.cs  # Wraps download logic
│   │
│   └── TidalarrModule.cs            # Plugin registration
│
├── TidalCLI/                         # NEW: Test bed CLI application
│   ├── Program.cs                    # Entry point with DI setup
│   ├── Commands/                     # CLI commands
│   │   ├── AuthCommands.cs          # login, logout, status
│   │   ├── SearchCommands.cs        # search, batch-search
│   │   ├── DownloadCommands.cs      # download album/track
│   │   ├── ConfigCommands.cs        # config management
│   │   └── TestCommands.cs          # testing utilities
│   ├── Services/                     # CLI-specific services
│   │   ├── PluginHost.cs            # Main plugin integration
│   │   ├── Adapters/                 # CLI → Plugin adapters
│   │   │   ├── CliLoggerAdapter.cs  # ILogger bridge
│   │   │   ├── CliCacheAdapter.cs   # Caching implementation
│   │   │   └── CliHttpAdapter.cs    # HttpClient wrapper
│   │   ├── CliDownloadService.cs    # CLI download orchestration
│   │   └── CliConfigService.cs      # Configuration management
│   ├── Models/                       # CLI models
│   │   └── TidalConfig.cs           # Unified configuration
│   └── TidalCLI.csproj              # References main plugin
│
├── docs/
│   ├── TECH-DEBT-INVENTORY.md       # Explicit tech debt tracking
│   ├── PORTING-NOTES.md             # Specific changes made during port
│   └── SHARED-LIBRARY-PROPOSAL.md   # NEW: Qobuzarr refactoring plan
│
└── plugin.json                       # Lidarr plugin manifest
```

---

## 2. TidalCLI Implementation

### 2.1 Architecture Pattern (Plugin-First)

```
TidalCLI Commands → PluginHost → Plugin Services → Tidal API
       ↓                ↓              ↓
  CLI-specific     Adapter Layer   Core Logic
   interfaces       (bridges)      (from plugin)
```

### 2.2 Core Components

#### Program.cs - Entry Point
```csharp
using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

class Program
{
    static async Task<int> Main(string[] args)
    {
        // Setup DI container
        var services = new ServiceCollection();
        ConfigureServices(services);
        var provider = services.BuildServiceProvider();
        
        // Build command structure
        var rootCommand = new RootCommand("Tidalarr CLI - Test bed for Tidal integration");
        
        // Add command groups
        rootCommand.AddCommand(AuthCommands.Build(provider));
        rootCommand.AddCommand(SearchCommands.Build(provider));
        rootCommand.AddCommand(DownloadCommands.Build(provider));
        rootCommand.AddCommand(ConfigCommands.Build(provider));
        rootCommand.AddCommand(TestCommands.Build(provider));
        
        return await rootCommand.InvokeAsync(args);
    }
    
    static void ConfigureServices(IServiceCollection services)
    {
        // Plugin services (using adapters)
        services.AddSingleton<IPluginHost, PluginHost>();
        services.AddSingleton<ITidalSessionAdapter>(sp => 
            sp.GetRequiredService<IPluginHost>().SessionAdapter);
        services.AddSingleton<ITidalApiAdapter>(sp => 
            sp.GetRequiredService<IPluginHost>().ApiAdapter);
            
        // CLI-specific services
        services.AddSingleton<ICliLogger, CliLogger>();
        services.AddSingleton<ICliCache, CliCacheAdapter>();
        services.AddSingleton<ICliDownloadService, CliDownloadService>();
        services.AddSingleton<ICliConfigService, CliConfigService>();
    }
}
```

#### PluginHost.cs - Plugin Integration Point
```csharp
public class PluginHost : IPluginHost
{
    private readonly TidalSession _tidalSession;
    private readonly TidalAPI _tidalApi;
    private readonly TidalSessionAdapter _sessionAdapter;
    private readonly TidalApiAdapter _apiAdapter;
    private readonly TidalDownloadAdapter _downloadAdapter;
    
    public PluginHost(ICliLogger logger, ICliCache cache, IHttpClientFactory httpClientFactory)
    {
        // Initialize TidalSharp components
        _tidalSession = new TidalSession();
        _tidalApi = new TidalAPI(_tidalSession);
        
        // Create adapters
        _sessionAdapter = new TidalSessionAdapter(_tidalSession);
        _apiAdapter = new TidalApiAdapter(_tidalApi, _tidalSession);
        _downloadAdapter = new TidalDownloadAdapter(_tidalApi, _tidalSession);
    }
    
    public ITidalSessionAdapter SessionAdapter => _sessionAdapter;
    public ITidalApiAdapter ApiAdapter => _apiAdapter;
    public ITidalDownloadAdapter DownloadAdapter => _downloadAdapter;
    
    public async Task<bool> AuthenticateAsync(string username, string password)
    {
        return await _sessionAdapter.AuthenticateAsync(username, password);
    }
    
    public async Task<bool> AuthenticateOAuthAsync()
    {
        // Show OAuth URL and wait for callback
        var (codeVerifier, codeChallenge) = GeneratePKCE();
        var authUrl = _tidalSession.GetOAuthLoginUrl(codeChallenge);
        
        AnsiConsole.MarkupLine($"[yellow]Open this URL in your browser:[/]");
        AnsiConsole.MarkupLine($"[blue]{authUrl}[/]");
        
        var authCode = AnsiConsole.Ask<string>("Enter the authorization code:");
        return await _sessionAdapter.AuthenticateOAuthAsync(authCode, codeVerifier);
    }
}
```

### 2.3 Commands Implementation

#### Authentication Commands
```csharp
public static class AuthCommands
{
    public static Command Build(IServiceProvider services)
    {
        var authCommand = new Command("auth", "Authentication management");
        
        // Login command
        var loginCommand = new Command("login", "Authenticate with Tidal");
        loginCommand.AddOption(new Option<string>("--username", "Email or username"));
        loginCommand.AddOption(new Option<string>("--password", "Password"));
        loginCommand.AddOption(new Option<bool>("--oauth", "Use OAuth browser flow"));
        
        loginCommand.SetHandler(async (string username, string password, bool oauth) =>
        {
            var host = services.GetRequiredService<IPluginHost>();
            
            if (oauth)
            {
                var success = await host.AuthenticateOAuthAsync();
                AnsiConsole.MarkupLine(success ? "[green]✓ Authenticated[/]" : "[red]✗ Failed[/]");
            }
            else
            {
                username ??= AnsiConsole.Ask<string>("Email:");
                password ??= AnsiConsole.Prompt(new TextPrompt<string>("Password:").Secret());
                
                var success = await host.AuthenticateAsync(username, password);
                AnsiConsole.MarkupLine(success ? "[green]✓ Authenticated[/]" : "[red]✗ Failed[/]");
            }
        });
        
        authCommand.AddCommand(loginCommand);
        return authCommand;
    }
}
```

#### Search Commands
```csharp
public static class SearchCommands
{
    public static Command Build(IServiceProvider services)
    {
        var searchCommand = new Command("search", "Search Tidal content");
        searchCommand.AddArgument(new Argument<string>("query", "Search query"));
        searchCommand.AddOption(new Option<string>("--type", "Type: album, track, artist"));
        searchCommand.AddOption(new Option<int>("--limit", () => 10, "Result limit"));
        
        searchCommand.SetHandler(async (string query, string type, int limit) =>
        {
            var api = services.GetRequiredService<IPluginHost>().ApiAdapter;
            var results = await api.SearchAsync(query, type, limit);
            
            // Display results in table
            var table = new Table();
            table.AddColumn("Type");
            table.AddColumn("Title");
            table.AddColumn("Artist");
            table.AddColumn("Quality");
            
            foreach (var result in results)
            {
                table.AddRow(result.Type, result.Title, result.Artist, result.Quality);
            }
            
            AnsiConsole.Write(table);
        });
        
        return searchCommand;
    }
}
```

#### Download Commands
```csharp
public static class DownloadCommands
{
    public static Command Build(IServiceProvider services)
    {
        var downloadCommand = new Command("download", "Download content");
        
        var albumCommand = new Command("album", "Download album");
        albumCommand.AddArgument(new Argument<string>("id", "Album ID"));
        albumCommand.AddOption(new Option<string>("--quality", "Quality: master, lossless, high"));
        albumCommand.AddOption(new Option<string>("--output", "Output directory"));
        
        albumCommand.SetHandler(async (string id, string quality, string output) =>
        {
            var downloader = services.GetRequiredService<IPluginHost>().DownloadAdapter;
            var config = services.GetRequiredService<ICliConfigService>().GetConfig();
            
            output ??= config.DownloadPath;
            quality ??= config.PreferredQuality;
            
            await AnsiConsole.Progress()
                .Start(async ctx =>
                {
                    var task = ctx.AddTask($"Downloading album {id}");
                    
                    await downloader.DownloadAlbumAsync(id, quality, output, 
                        progress => task.Value = progress);
                });
        });
        
        downloadCommand.AddCommand(albumCommand);
        return downloadCommand;
    }
}
```

### 2.4 Test Commands

```csharp
public static class TestCommands
{
    public static Command Build(IServiceProvider services)
    {
        var testCommand = new Command("test", "Testing utilities");
        
        // Test authentication flow
        var authTestCommand = new Command("auth-flow", "Test complete auth flow");
        authTestCommand.SetHandler(async () =>
        {
            var host = services.GetRequiredService<IPluginHost>();
            
            AnsiConsole.Status()
                .Start("Testing authentication...", async ctx =>
                {
                    // Test OAuth flow
                    ctx.Status("Testing OAuth PKCE generation...");
                    var pkce = TestPKCEGeneration();
                    
                    // Test session management
                    ctx.Status("Testing session refresh...");
                    var refresh = await TestSessionRefresh(host);
                    
                    // Test API authentication
                    ctx.Status("Testing API access...");
                    var api = await TestApiAccess(host);
                    
                    // Display results
                    var table = new Table();
                    table.AddColumn("Test");
                    table.AddColumn("Result");
                    table.AddRow("PKCE Generation", pkce ? "[green]✓[/]" : "[red]✗[/]");
                    table.AddRow("Session Refresh", refresh ? "[green]✓[/]" : "[red]✗[/]");
                    table.AddRow("API Access", api ? "[green]✓[/]" : "[red]✗[/]");
                    
                    AnsiConsole.Write(table);
                });
        });
        
        // Test download pipeline
        var downloadTestCommand = new Command("download-pipeline", "Test download components");
        downloadTestCommand.SetHandler(async () =>
        {
            var host = services.GetRequiredService<IPluginHost>();
            
            await AnsiConsole.Status()
                .Start("Testing download pipeline...", async ctx =>
                {
                    // Test manifest parsing
                    ctx.Status("Testing MPD manifest parsing...");
                    var mpd = TestMPDParsing();
                    
                    // Test chunk downloading
                    ctx.Status("Testing chunk download...");
                    var chunks = await TestChunkDownload(host);
                    
                    // Test decryption
                    ctx.Status("Testing decryption...");
                    var decrypt = TestDecryption();
                    
                    // Display results
                    AnsiConsole.Write(new Rule("[yellow]Download Pipeline Test Results[/]"));
                    AnsiConsole.MarkupLine($"MPD Parsing: {(mpd ? "[green]✓[/]" : "[red]✗[/]")}");
                    AnsiConsole.MarkupLine($"Chunk Download: {(chunks ? "[green]✓[/]" : "[red]✗[/]")}");
                    AnsiConsole.MarkupLine($"Decryption: {(decrypt ? "[green]✓[/]" : "[red]✗[/]")}");
                });
        });
        
        // Test Lidarr integration
        var lidarrTestCommand = new Command("lidarr-integration", "Test Lidarr compatibility");
        lidarrTestCommand.SetHandler(async () =>
        {
            // Test indexer implementation
            // Test download client implementation
            // Test metadata mapping
        });
        
        testCommand.AddCommand(authTestCommand);
        testCommand.AddCommand(downloadTestCommand);
        testCommand.AddCommand(lidarrTestCommand);
        
        return testCommand;
    }
}
```

### 2.5 Configuration Management

```csharp
public class TidalConfig
{
    // Authentication
    public string Username { get; set; }
    public string Password { get; set; }
    public string AccessToken { get; set; }
    public string RefreshToken { get; set; }
    public bool UseOAuth { get; set; } = true;
    
    // Quality Settings
    public string PreferredQuality { get; set; } = "lossless";
    public bool EnableQualityFallback { get; set; } = true;
    
    // Download Settings
    public string DownloadPath { get; set; } = "./downloads";
    public int MaxConcurrentDownloads { get; set; } = 4;
    public string FileNamePattern { get; set; } = "{artist} - {album}/{track} - {title}";
    
    // API Settings
    public int RequestTimeout { get; set; } = 30000;
    public int RetryCount { get; set; } = 3;
    public int CacheDuration { get; set; } = 900; // 15 minutes
    
    // Development
    public bool DebugMode { get; set; } = false;
    public bool VerboseLogging { get; set; } = false;
}
```

---

## 3. Strategic Qobuzarr Refactoring Proposal

### 3.1 Extract Shared Lidarr Plugin Base Library

**Create: `Lidarr.Plugin.Common`**

This is the highest-value refactoring that would benefit both Qobuzarr and Tidalarr by extracting common Lidarr integration patterns.

#### Proposed Structure:
```csharp
// Lidarr.Plugin.Common/Base/BaseStreamingIndexer.cs
public abstract class BaseStreamingIndexer<TSettings> : HttpIndexerBase<TSettings> 
    where TSettings : IIndexerSettings, new()
{
    protected abstract Task<IList<StreamingRelease>> SearchServiceAsync(string query);
    protected abstract ReleaseInfo MapToRelease(StreamingRelease release);
    
    protected override async Task<IList<ReleaseInfo>> FetchReleases(IndexerRequest request)
    {
        var releases = await SearchServiceAsync(request.SearchCriteria.SearchTerm);
        return releases.Select(MapToRelease).ToList();
    }
    
    // Common validation, error handling, logging
}

// Lidarr.Plugin.Common/Base/BaseStreamingDownloadClient.cs
public abstract class BaseStreamingDownloadClient<TSettings> : DownloadClientBase<TSettings>
    where TSettings : IProviderConfig, new()
{
    protected abstract Task<byte[]> DownloadTrackAsync(string trackId, string quality);
    protected abstract Task ApplyMetadataAsync(string filePath, TrackMetadata metadata);
    
    public override async Task<string> Download(RemoteAlbum remoteAlbum)
    {
        // Common download orchestration
        // Error handling, retry logic
        // Progress reporting
    }
}

// Lidarr.Plugin.Common/Models/
public class StreamingRelease { /* Common release model */ }
public class TrackMetadata { /* Common metadata model */ }
public class StreamingQuality { /* Quality definitions */ }

// Lidarr.Plugin.Common/Services/
public interface IAuthenticationService<TSession> { /* Common auth interface */ }
public interface IStreamingApiClient { /* Common API interface */ }
public interface ICacheService { /* Common caching */ }

// Lidarr.Plugin.Common/Settings/
public abstract class BaseStreamingSettings : IIndexerSettings
{
    [FieldDefinition(1, Label = "Preferred Quality")]
    public string PreferredQuality { get; set; }
    
    [FieldDefinition(2, Label = "Enable Cache")]
    public bool EnableCache { get; set; } = true;
    
    // Other common settings
}
```

#### Benefits:
- **60% code reduction** in both plugins
- **Consistent behavior** across all streaming plugins
- **Shared bug fixes** benefit both projects
- **Faster development** of future plugins (Spotify, Apple Music, etc.)
- **Standardized testing** infrastructure

### 3.2 Implementation Plan for Shared Library

#### Phase 1: Extract Common Code (Week 1)
1. Create new `Lidarr.Plugin.Common` project
2. Extract base classes from Qobuzarr
3. Define common interfaces
4. Create shared models

#### Phase 2: Refactor Qobuzarr (Week 2)
1. Update Qobuzarr to use shared library
2. Remove duplicated code
3. Test thoroughly
4. Update documentation

#### Phase 3: Build Tidalarr on Shared Base (Week 3-4)
1. Implement Tidalarr using shared base classes
2. Only write Tidal-specific code
3. Benefit from tested common functionality

### 3.3 Other Beneficial Refactorings

#### Extract Response Caching
```csharp
// Lidarr.Plugin.Common/Caching/ResponseCache.cs
public class ResponseCache : IResponseCache
{
    public async Task<T> GetOrAddAsync<T>(string key, Func<Task<T>> factory, TimeSpan ttl)
    {
        // Thread-safe caching with TTL
    }
}
```

#### Extract Authentication Framework
```csharp
// Lidarr.Plugin.Common/Authentication/SessionManager.cs
public abstract class SessionManager<TSession> where TSession : ISession
{
    protected abstract Task<TSession> CreateSessionAsync(ICredentials credentials);
    protected abstract Task<TSession> RefreshSessionAsync(TSession session);
    
    public async Task<TSession> GetValidSessionAsync()
    {
        // Common session management, refresh logic
    }
}
```

#### Extract Download Orchestration
```csharp
// Lidarr.Plugin.Common/Download/DownloadOrchestrator.cs
public class DownloadOrchestrator
{
    public async Task<DownloadResult> ProcessAsync(DownloadRequest request)
    {
        // Queue management
        // Concurrent download control
        // Retry logic
        // Progress reporting
    }
}
```

---

## 4. Updated Implementation Timeline

### With Shared Library Approach:

#### Week 1: Qobuzarr Refactoring
- Extract common code to shared library
- Test Qobuzarr with shared library

#### Week 2: TidalCore Port
- Port TidalSharp code to TidalCore
- Create adapter layer

#### Week 3: Tidalarr Integration
- Implement Tidalarr using shared library
- Only write Tidal-specific adapters

#### Week 4: TidalCLI Development
- Build CLI following QobuzCLI pattern
- Add comprehensive test commands

#### Week 5: Testing & Polish
- End-to-end testing
- Documentation
- Package for release

---

## 5. Benefits of This Approach

### Immediate Benefits:
1. **Reduced Development Time**: 5 weeks instead of 4, but with much less code
2. **Higher Quality**: Leveraging tested common code
3. **Better Testing**: TidalCLI provides comprehensive test bed
4. **Consistency**: Both plugins behave the same way

### Long-term Benefits:
1. **Easier Maintenance**: Fix once, benefit everywhere
2. **Future Plugins**: Next streaming service in 2 weeks, not 4
3. **Community Contribution**: Easier for others to add providers
4. **Professional Architecture**: Clean, maintainable, enterprise-grade

---

## 6. Decision Matrix

| Approach | Dev Time | Tech Debt | Maintenance | Future Plugins |
|----------|----------|-----------|-------------|----------------|
| Original V2 (Port only) | 4 weeks | High | Hard | 4 weeks each |
| V2 + TidalCLI | 4.5 weeks | High | Medium | 4 weeks each |
| **V2 + Shared Library** | 5 weeks | Low | Easy | 2 weeks each |

---

## Conclusion

By adding TidalCLI and investing in the shared library refactoring, we:
1. Get a robust test bed for development
2. Reduce technical debt significantly
3. Create a foundation for future streaming plugins
4. Maintain both plugins more easily
5. Build a professional, scalable architecture

The extra week of investment pays massive dividends in reduced maintenance and faster future development.