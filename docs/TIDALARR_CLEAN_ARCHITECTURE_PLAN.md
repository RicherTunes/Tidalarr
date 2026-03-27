> **Note:** This document is historical and may not reflect current architecture. It was written during the planning phase before implementation began. The actual codebase has evolved beyond this plan. See CLAUDE.md for current guidance.

# Tidalarr Clean Architecture Implementation Plan
## No TidalSharp Dependency - Clean Component Design

---

## Executive Summary

This plan reimplements Tidal functionality using TidalSharp as a **reference only**, not a dependency. We'll break down TidalSharp's monolithic god classes into focused, well-organized components while leveraging the shared library for all Lidarr integration.

**Approach**: Extract knowledge from TidalSharp, implement cleanly with proper separation of concerns.

---

## 1. Clean Architecture Overview

```
┌─────────────────────────────────────────────┐
│           Lidarr Integration Layer          │
│     (TidalIndexer, TidalDownloadClient)     │
│                                             │
│  Uses: Lidarr.Plugin.Common (NuGet)        │
└─────────────────────────────────────────────┘
                        │
┌─────────────────────────────────────────────┐
│         Application Services Layer          │
│    (TidalSearchService, TidalDownloadService)│
│                                             │
│  Contains: Use cases and orchestration      │
└─────────────────────────────────────────────┘
                        │
┌─────────────────────────────────────────────┐
│            Domain Services Layer            │
│  (TidalApiClient, TidalAuthService, etc.)   │
│                                             │
│  Contains: Clean, focused business logic    │
└─────────────────────────────────────────────┘
                        │
┌─────────────────────────────────────────────┐
│         Infrastructure Layer                │
│  (HttpClient, Json, FileSystem, etc.)       │
│                                             │
│  Contains: External integrations only       │
└─────────────────────────────────────────────┘
```

---

## 2. Breaking Down TidalSharp God Classes

### 2.1 TidalSharp Analysis - What to Extract

**From TidalSharp `API.cs` (monolithic ~400 lines)** → Break into:
- `TidalApiClient` (HTTP operations)
- `TidalEndpoints` (URL management)  
- `TidalRequestBuilder` (request construction)
- `TidalResponseParser` (response parsing)
- `TidalErrorHandler` (error classification)

**From TidalSharp `Session.cs` (monolithic ~300 lines)** → Break into:
- `TidalOAuthService` (OAuth PKCE flow)
- `TidalTokenManager` (token lifecycle)
- `TidalSessionManager` (session state)
- `PKCEGenerator` (PKCE challenge generation)

**From TidalSharp `Downloader.cs` (monolithic ~400 lines)** → Break into:
- `TidalStreamService` (stream URL acquisition)
- `TidalManifestParser` (manifest processing)  
- `TidalChunkDownloader` (chunk assembly)
- `TidalDecryptor` (stream decryption)
- `TidalMetadataApplier` (tag application)

---

## 3. Clean Component Design

### 3.1 Authentication Components

```csharp
// Domain/Authentication/TidalOAuthService.cs - Clean, focused OAuth implementation
public class TidalOAuthService
{
    private readonly HttpClient _httpClient;
    private readonly PKCEGenerator _pkceGenerator;
    
    public async Task<TidalAuthUrl> GenerateAuthUrlAsync()
    {
        var (verifier, challenge) = _pkceGenerator.GeneratePair();
        var state = GenerateSecureState();
        
        var authUrl = BuildAuthorizationUrl(challenge, state);
        
        return new TidalAuthUrl(authUrl, verifier, state);
    }
    
    public async Task<TidalTokens> ExchangeCodeAsync(string authCode, string codeVerifier)
    {
        var request = BuildTokenRequest(authCode, codeVerifier);
        var response = await _httpClient.SendAsync(request);
        
        var tokenData = await ParseTokenResponse(response);
        return MapToTidalTokens(tokenData);
    }
    
    // Single responsibility: OAuth flow only
}

// Domain/Authentication/TidalTokenManager.cs - Clean token lifecycle
public class TidalTokenManager
{
    private readonly ITokenStorage _storage;
    private readonly TidalOAuthService _oauthService;
    
    public async Task<TidalTokens> GetValidTokensAsync()
    {
        var tokens = await _storage.LoadTokensAsync();
        
        if (tokens == null)
            throw new TidalNotAuthenticatedException();
            
        if (tokens.IsExpired)
            tokens = await RefreshTokensAsync(tokens);
            
        return tokens;
    }
    
    private async Task<TidalTokens> RefreshTokensAsync(TidalTokens tokens)
    {
        var refreshedTokens = await _oauthService.RefreshAsync(tokens.RefreshToken);
        await _storage.SaveTokensAsync(refreshedTokens);
        return refreshedTokens;
    }
    
    // Single responsibility: Token management only
}

// Domain/Authentication/PKCEGenerator.cs - Clean PKCE implementation  
public class PKCEGenerator
{
    public (string codeVerifier, string codeChallenge) GeneratePair()
    {
        var codeVerifier = GenerateCodeVerifier(128);
        var codeChallenge = CreateS256Challenge(codeVerifier);
        return (codeVerifier, codeChallenge);
    }
    
    private string CreateS256Challenge(string codeVerifier)
    {
        using var sha256 = SHA256.Create();
        var challengeBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(codeVerifier));
        return Base64UrlEncode(challengeBytes);
    }
    
    // Single responsibility: PKCE generation only
}
```

### 3.2 API Client Components

```csharp
// Domain/Api/TidalApiClient.cs - Clean API client
public class TidalApiClient
{
    private readonly HttpClient _httpClient;
    private readonly TidalTokenManager _tokenManager;
    private readonly TidalRequestBuilder _requestBuilder;
    private readonly TidalResponseParser _responseParser;
    
    public async Task<TidalTrack> GetTrackAsync(string trackId)
    {
        var tokens = await _tokenManager.GetValidTokensAsync();
        var request = _requestBuilder.BuildGetTrackRequest(trackId, tokens);
        var response = await _httpClient.SendAsync(request);
        
        return _responseParser.ParseTrack(response);
    }
    
    public async Task<TidalSearchResults> SearchAsync(string query, int limit = 100)
    {
        var tokens = await _tokenManager.GetValidTokensAsync();
        var request = _requestBuilder.BuildSearchRequest(query, limit, tokens);
        var response = await _httpClient.SendAsync(request);
        
        return _responseParser.ParseSearchResults(response);
    }
    
    // Single responsibility: API operations only
}

// Domain/Api/TidalRequestBuilder.cs - Clean request construction
public class TidalRequestBuilder
{
    public HttpRequestMessage BuildSearchRequest(string query, int limit, TidalTokens tokens)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, TidalEndpoints.Search);
        
        AddAuthenticationHeaders(request, tokens);
        AddSearchParameters(request, query, limit, tokens.SessionId, tokens.CountryCode);
        
        return request;
    }
    
    public HttpRequestMessage BuildGetStreamUrlRequest(string trackId, TidalQuality quality, TidalTokens tokens)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, TidalEndpoints.GetPlaybackInfo(trackId));
        
        AddAuthenticationHeaders(request, tokens);
        AddStreamParameters(request, quality, tokens.SessionId, tokens.CountryCode);
        
        return request;
    }
    
    // Single responsibility: Request building only
}

// Domain/Api/TidalResponseParser.cs - Clean response parsing
public class TidalResponseParser
{
    public TidalTrack ParseTrack(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        var trackData = JsonSerializer.Deserialize<TidalTrackDto>(json);
        
        return MapToTidalTrack(trackData);
    }
    
    public TidalSearchResults ParseSearchResults(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        var searchData = JsonSerializer.Deserialize<TidalSearchDto>(json);
        
        return new TidalSearchResults
        {
            Albums = searchData.albums.items.Select(MapToTidalAlbum).ToList(),
            Tracks = searchData.tracks.items.Select(MapToTidalTrack).ToList()
        };
    }
    
    // Single responsibility: Response parsing only
}
```

### 3.3 Streaming Components  

```csharp
// Domain/Streaming/TidalStreamService.cs - Clean stream acquisition
public class TidalStreamService
{
    private readonly TidalApiClient _apiClient;
    private readonly TidalManifestParser _manifestParser;
    
    public async Task<TidalStreamInfo> GetStreamInfoAsync(string trackId, TidalQuality quality)
    {
        var playbackInfo = await _apiClient.GetPlaybackInfoAsync(trackId, quality);
        var manifest = _manifestParser.ParseManifest(playbackInfo.Manifest, playbackInfo.ManifestMimeType);
        
        return new TidalStreamInfo
        {
            ChunkUrls = manifest.ChunkUrls,
            FileExtension = manifest.FileExtension,
            IsEncrypted = playbackInfo.EncryptionType != "NONE",
            SecurityToken = playbackInfo.SecurityToken
        };
    }
    
    // Single responsibility: Stream URL management
}

// Domain/Streaming/TidalManifestParser.cs - Clean manifest parsing
public class TidalManifestParser
{
    public TidalManifest ParseManifest(string encodedManifest, string mimeType)
    {
        var manifestData = Convert.FromBase64String(encodedManifest);
        var decodedManifest = Encoding.UTF8.GetString(manifestData);
        
        return mimeType switch
        {
            "application/dash+xml" => ParseDashManifest(decodedManifest),
            "application/vnd.tidal.bts" => ParseBtsManifest(decodedManifest),
            _ => throw new UnsupportedManifestException($"Unknown manifest type: {mimeType}")
        };
    }
    
    private TidalManifest ParseDashManifest(string xmlContent)
    {
        // Clean MPD XML parsing - extract from TidalSharp MPD.cs
    }
    
    // Single responsibility: Manifest parsing only
}

// Domain/Streaming/TidalChunkDownloader.cs - Clean chunk assembly  
public class TidalChunkDownloader
{
    private readonly HttpClient _httpClient;
    private readonly TidalDecryptor _decryptor;
    
    public async Task<Stream> DownloadAndAssembleAsync(TidalStreamInfo streamInfo, IProgress<int> progress = null)
    {
        var chunks = await DownloadChunksAsync(streamInfo.ChunkUrls, progress);
        var assembledStream = AssembleChunks(chunks);
        
        if (streamInfo.IsEncrypted)
            return _decryptor.DecryptStream(assembledStream, streamInfo.SecurityToken);
            
        return assembledStream;
    }
    
    private async Task<List<byte[]>> DownloadChunksAsync(string[] urls, IProgress<int> progress)
    {
        var chunks = new List<byte[]>();
        
        // CRITICAL: Download chunks sequentially to preserve order
        for (int i = 0; i < urls.Length; i++)
        {
            var chunk = await _httpClient.GetByteArrayAsync(urls[i]);
            chunks.Add(chunk);
            progress?.Report(i + 1);
        }
        
        return chunks;
    }
    
    // Single responsibility: Chunk downloading and assembly
}
```

---

## 4. Revised Project Structure

```
Tidalarr/
├── src/
│   ├── Tidalarr.csproj                    # NuGet: Lidarr.Plugin.Common only
│   │
│   ├── Domain/                            # Clean business logic
│   │   ├── Authentication/
│   │   │   ├── TidalOAuthService.cs       # OAuth flow (80 lines)
│   │   │   ├── TidalTokenManager.cs       # Token lifecycle (60 lines)
│   │   │   ├── PKCEGenerator.cs           # PKCE generation (40 lines)
│   │   │   └── TidalSessionManager.cs     # Session state (50 lines)
│   │   ├── Api/
│   │   │   ├── TidalApiClient.cs          # API operations (120 lines)
│   │   │   ├── TidalRequestBuilder.cs     # Request construction (80 lines)
│   │   │   ├── TidalResponseParser.cs     # Response parsing (100 lines)
│   │   │   └── TidalEndpoints.cs          # URL definitions (30 lines)
│   │   ├── Streaming/
│   │   │   ├── TidalStreamService.cs      # Stream acquisition (70 lines)
│   │   │   ├── TidalManifestParser.cs     # Manifest parsing (90 lines)
│   │   │   ├── TidalChunkDownloader.cs    # Chunk download (80 lines)
│   │   │   └── TidalDecryptor.cs          # Stream decryption (60 lines)
│   │   ├── Models/                        # Clean data models
│   │   │   ├── TidalModels.cs             # Core models (150 lines)
│   │   │   └── TidalDtos.cs               # API response DTOs (100 lines)
│   │   └── Constants/
│   │       └── TidalConstants.cs          # API endpoints, keys (40 lines)
│   │
│   ├── Application/                       # Use cases and orchestration
│   │   ├── Services/
│   │   │   ├── TidalSearchService.cs      # Search orchestration (100 lines)
│   │   │   ├── TidalDownloadService.cs    # Download orchestration (120 lines)
│   │   │   └── TidalMetadataService.cs    # Metadata operations (80 lines)
│   │   ├── Validators/
│   │   │   └── TidalInputValidator.cs     # Input validation (40 lines)
│   │   └── Mappers/
│   │       └── TidalModelMapper.cs        # DTO to domain mapping (60 lines)
│   │
│   ├── Infrastructure/                    # External concerns
│   │   ├── Http/
│   │   │   ├── TidalHttpClient.cs         # HTTP wrapper (60 lines)
│   │   │   └── HttpClientExtensions.cs    # HTTP utilities (40 lines)
│   │   ├── Storage/
│   │   │   └── JsonTokenStorage.cs        # Token persistence (50 lines)
│   │   ├── Resilience/
│   │   │   ├── TidalCircuitBreaker.cs     # Circuit breaker (40 lines)
│   │   │   └── RetryPolicies.cs           # Polly policies (30 lines)
│   │   └── Serialization/
│   │       └── JsonSerializerConfig.cs    # JSON settings (20 lines)
│   │
│   ├── Integration/                       # Lidarr plugin interfaces
│   │   ├── TidalIndexer.cs               # Uses shared library (80 lines)
│   │   ├── TidalDownloadClient.cs        # Uses shared library (100 lines)
│   │   ├── TidalSettings.cs              # Extends BaseStreamingSettings (60 lines)
│   │   └── TidalModule.cs                # DI registration (40 lines)
│   │
│   └── Health/                           # Monitoring
│       ├── TidalHealthCheck.cs           # Health monitoring (30 lines)
│       └── TidalTelemetry.cs             # Metrics collection (40 lines)
│
├── tests/                                # Clean test structure
│   ├── Domain.Tests/                     # Domain logic tests
│   ├── Integration.Tests/                # Real API tests
│   └── Load.Tests/                       # Performance tests
│
└── TidalCLI/                             # Test bed application
    └── [Same structure as planned]
```

**Total Implementation**: ~1,700 lines (vs 3,500+ traditional, ~1,200 with TidalSharp port)
**Benefits**: Clean, maintainable, testable components with single responsibilities

---

## 5. Key Implementation Extracts from TidalSharp

### 5.1 Critical Values to Preserve (from TidalSharp Globals.cs)

```csharp
// Domain/Constants/TidalConstants.cs - Only the working values
public static class TidalConstants
{
    // Client credentials - MUST match TidalSharp exactly
    public const string CLIENT_ID_PKCE = "6BDSRdpK9hqEBTgU";
    public const string CLIENT_SECRET_PKCE = "xeuPmY7nbpZ9IIbLAcQ93shka1VNheUAqN6IcszjTG8=";
    public const string REDIRECT_URI = "https://tidal.com/android/login/auth";
    
    // API endpoints - Keep v1 (working)
    public const string API_V1_BASE = "https://api.tidal.com/v1/";
    public const string AUTH_BASE = "https://auth.tidal.com/v1/oauth2/token";
    public const string LOGIN_BASE = "https://login.tidal.com/authorize";
    
    // Decryption key - CRITICAL for encrypted streams
    public const string MASTER_KEY = "UIlTTEMmmLfGowo/UC60x2H45W6MdGgTRfo/umg4754=";
    
    // Quality mappings
    public static readonly Dictionary<TidalQuality, string> QualityParameters = new()
    {
        [TidalQuality.Low] = "LOW",
        [TidalQuality.High] = "HIGH", 
        [TidalQuality.Lossless] = "LOSSLESS",
        [TidalQuality.HiRes] = "HI_RES_LOSSLESS"
    };
}
```

### 5.2 Clean OAuth Implementation (from Session.cs)

```csharp
// Domain/Authentication/TidalOAuthService.cs
public class TidalOAuthService
{
    public string BuildAuthorizationUrl(string codeChallenge, string state)
    {
        var parameters = new Dictionary<string, string>
        {
            ["response_type"] = "code",
            ["redirect_uri"] = TidalConstants.REDIRECT_URI,
            ["client_id"] = TidalConstants.CLIENT_ID_PKCE,
            ["lang"] = "EN",
            ["appMode"] = "android",
            ["client_unique_key"] = GenerateClientUniqueKey(),
            ["code_challenge"] = codeChallenge,
            ["code_challenge_method"] = "S256",
            ["restrict_signup"] = "true"
        };
        
        return $"{TidalConstants.LOGIN_BASE}?{BuildQueryString(parameters)}";
    }
    
    public async Task<TidalTokens> ExchangeCodeForTokensAsync(string authCode, string codeVerifier)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, TidalConstants.AUTH_BASE);
        
        var formData = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("code", authCode),
            new KeyValuePair<string, string>("client_id", TidalConstants.CLIENT_ID_PKCE),
            new KeyValuePair<string, string>("grant_type", "authorization_code"),
            new KeyValuePair<string, string>("redirect_uri", TidalConstants.REDIRECT_URI),
            new KeyValuePair<string, string>("scope", "r_usr+w_usr+w_sub"),
            new KeyValuePair<string, string>("code_verifier", codeVerifier),
            new KeyValuePair<string, string>("client_secret", TidalConstants.CLIENT_SECRET_PKCE)
        });
        
        request.Content = formData;
        
        var response = await _httpClient.SendAsync(request);
        return await ParseTokenResponse(response);
    }
}
```

### 5.3 Clean Manifest Parser (from StreamManifest.cs)

```csharp
// Domain/Streaming/TidalManifestParser.cs
public class TidalManifestParser
{
    public TidalManifest ParseManifest(string encodedManifest, string mimeType)
    {
        var manifestData = Convert.FromBase64String(encodedManifest);
        var decodedManifest = Encoding.UTF8.GetString(manifestData);
        
        return mimeType switch
        {
            "application/dash+xml" => ParseDashManifest(decodedManifest),
            "application/vnd.tidal.bts" => ParseBtsManifest(decodedManifest),
            _ => throw new UnsupportedManifestException($"Unsupported manifest: {mimeType}")
        };
    }
    
    private TidalManifest ParseDashManifest(string xmlContent)
    {
        // Extract MPD parsing logic from TidalSharp MPD.cs and DashInfo.cs
        var doc = XDocument.Parse(xmlContent);
        var period = doc.Descendants("Period").First();
        var adaptationSet = period.Descendants("AdaptationSet").First();
        
        var chunkUrls = ExtractChunkUrls(adaptationSet);
        var codec = ExtractCodec(adaptationSet);
        var mimeType = ExtractMimeType(adaptationSet);
        
        return new TidalManifest
        {
            ChunkUrls = chunkUrls,
            Codec = codec,
            MimeType = mimeType,
            FileExtension = DetermineFileExtension(chunkUrls.First(), codec)
        };
    }
    
    // Single responsibility: Manifest parsing only
}
```

---

## 6. Shared Library Integration

### 6.1 Settings with Shared Library

```csharp
// Integration/TidalSettings.cs - Leverage shared library heavily
public class TidalSettings : BaseStreamingSettings, IIndexerSettings
{
    // Only Tidal-specific settings needed
    [FieldDefinition(10, Label = "Tidal Market", Type = FieldType.Select)]
    public string TidalMarket { get; set; } = "US";
    
    [FieldDefinition(11, Label = "Include MQA", Type = FieldType.Checkbox)]
    public bool IncludeMqa { get; set; } = true;
    
    [FieldDefinition(12, Label = "OAuth Redirect URL", Type = FieldType.Textbox,
                     HelpText = "Paste redirect URL from Tidal OAuth")]
    public string RedirectUrl { get; set; }
    
    // Inherit 90% from BaseStreamingSettings (authentication, quality, caching, etc.)
}
```

### 6.2 Indexer with Shared Patterns

```csharp
// Integration/TidalIndexer.cs - Clean integration
public class TidalIndexer : HttpIndexerBase<TidalSettings>
{
    private readonly TidalSearchService _searchService;     // Our clean service
    private readonly QualityMapper _qualityMapper;         // Shared library
    private readonly IStreamingResponseCache _cache;       // Shared library
    private readonly ICircuitBreaker _circuitBreaker;      // Our resilience
    
    protected override async Task<IList<ReleaseInfo>> FetchReleases(IndexerRequest request)
    {
        // Shared library caching pattern
        var cacheKey = $"tidal_search_{request.SearchCriteria.SearchTerm}";
        if (_cache.TryGet(cacheKey, out IList<ReleaseInfo> cached))
            return cached;
        
        // Our clean search service
        var searchResults = await _circuitBreaker.ExecuteAsync(async () =>
            await _searchService.SearchAsync(request.SearchCriteria.SearchTerm));
        
        // Shared library quality mapping
        var releases = searchResults.Albums.Select(album => new ReleaseInfo
        {
            Title = album.Title,
            Artist = string.Join(", ", album.Artists),
            Quality = _qualityMapper.MapToLidarrQuality(album.Quality),
            DownloadUrl = $"tidal://album/{album.Id}",
            // ... other mappings using shared patterns
        }).ToList();
        
        // Shared library caching
        _cache.Set(cacheKey, releases, TimeSpan.FromMinutes(Settings.CacheDuration));
        return releases;
    }
}
```

---

## 7. Implementation Timeline (3 Weeks)

### **Week 1: Clean Domain Components**
**Day 1**: Authentication components (OAuth, tokens, PKCE)  
**Day 2**: API client components (requests, responses, endpoints)  
**Day 3**: Streaming components (manifest, chunks, decryption)  
**Day 4**: Models and constants  
**Day 5**: Infrastructure layer (HTTP, storage, resilience)  

### **Week 2: Application + Integration**
**Day 1**: Application services (search, download, metadata)  
**Day 2**: Lidarr integration (indexer, download client, settings)  
**Day 3**: Dependency injection and module registration  
**Day 4**: Health checks and telemetry  
**Day 5**: Comprehensive testing  

### **Week 3: Polish + CLI**
**Day 1-2**: TidalCLI test bed application  
**Day 3**: Integration testing with real Tidal API  
**Day 4**: Performance testing and optimization  
**Day 5**: Documentation and release preparation  

---

## 8. Benefits of Clean Implementation

### **vs TidalSharp Port:**
- **Better maintainability** - Focused, single-responsibility classes
- **Easier testing** - Small, isolated components  
- **No god classes** - Clear separation of concerns
- **Future-proof** - Easy to enhance or replace individual components

### **vs From Scratch:**
- **Proven functionality** - Logic extracted from working TidalSharp
- **Critical values preserved** - All working API endpoints, keys, parameters
- **Reduced risk** - Known working patterns implemented cleanly

### **With Shared Library:**
- **74% code reduction** for Lidarr integration  
- **Professional patterns** for settings, caching, quality management
- **Consistent behavior** across streaming plugins
- **Battle-tested infrastructure** from Qobuzarr

---

## 9. Tech Debt Prevention Strategy

### 9.1 Domain Boundaries
- **No external dependencies** in Domain layer
- **Infrastructure isolated** behind interfaces
- **Application layer** orchestrates but doesn't implement
- **Clean dependency flow** inward only

### 9.2 Single Responsibility Principle
- **TidalOAuthService**: Only OAuth flow
- **TidalTokenManager**: Only token lifecycle  
- **TidalApiClient**: Only API operations
- **TidalManifestParser**: Only manifest parsing
- **Each class < 150 lines** with clear purpose

### 9.3 Testability
- **Every component mockable** through interfaces
- **No static dependencies** 
- **Pure functions** where possible
- **Clear input/output** contracts

---

## 10. Critical Implementation Notes

### 10.1 Must Preserve from TidalSharp
1. **Client credentials** (CLIENT_ID_PKCE, CLIENT_SECRET_PKCE)
2. **OAuth flow parameters** (exact redirect URI, scope, appMode)
3. **API endpoints** (v1 URLs, parameter names)
4. **Manifest parsing logic** (chunk URL extraction, codec detection)
5. **Decryption master key** (for encrypted streams)
6. **Quality detection logic** (tag-based quality identification)

### 10.2 Must Implement Clean
1. **Single-purpose classes** instead of monolithic god classes
2. **Interface-based design** for testability and maintainability  
3. **Dependency injection** throughout
4. **Error handling** with proper exception hierarchy
5. **Logging and telemetry** for observability

---

## 11. Success Criteria

### **V1 MVP**
- [ ] Clean OAuth authentication working
- [ ] Search functionality with quality detection
- [ ] Download functionality with sequential chunk assembly
- [ ] Metadata application to downloaded files
- [ ] Circuit breaker preventing API failures
- [ ] Health checks for monitoring
- [ ] Unit tests for all domain components

### **Production Ready**
- [ ] Integration tests with real Tidal API
- [ ] Performance benchmarks within acceptable ranges
- [ ] Error handling for all edge cases
- [ ] Telemetry and monitoring operational
- [ ] Documentation complete

**Code Quality Goals:**
- **No class > 150 lines** 
- **No method > 30 lines**
- **Single responsibility** per class
- **100% interface-based** dependency injection
- **Full test coverage** of domain logic

This clean architecture approach eliminates technical debt while preserving all the proven functionality from TidalSharp, resulting in a maintainable, professional implementation that leverages the shared library's benefits.
