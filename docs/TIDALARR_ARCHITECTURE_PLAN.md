# Tidalarr Architecture Plan
## Complete Blueprint for Tidal Integration Following Qobuzarr Architecture

---

## Executive Summary

Tidalarr will be a Lidarr plugin that provides seamless integration with Tidal, following the proven architectural patterns from Qobuzarr while implementing Tidal's OAuth 2.0 PKCE authentication and DASH streaming protocols. This document outlines the complete implementation strategy, component architecture, and technical specifications.

---

## 1. Project Structure

```
Tidalarr/
├── src/                              # Core plugin implementation
│   ├── API/                          # Tidal API client implementation
│   │   ├── ITidalApiClient.cs        # API client contract
│   │   ├── TidalApiClient.cs         # Main API orchestrator
│   │   ├── TidalHttpClient.cs        # HTTP operations
│   │   ├── TidalOAuthManager.cs      # OAuth 2.0 PKCE implementation
│   │   ├── TidalRequestSigner.cs     # Request authentication
│   │   └── TidalResponseCache.cs     # Response caching
│   ├── Authentication/               # Authentication system
│   │   ├── ITidalAuthenticationService.cs
│   │   ├── TidalAuthenticationService.cs
│   │   ├── TidalSession.cs           # Session management
│   │   ├── TidalTokenManager.cs      # Token refresh logic
│   │   └── PKCEGenerator.cs          # PKCE challenge generation
│   ├── Indexers/                     # Search implementation
│   │   ├── TidalIndexer.cs           # Main indexer
│   │   ├── TidalRequestGenerator.cs  # Search query builder
│   │   ├── TidalParser.cs            # Response parser
│   │   └── TidalQualityDetector.cs   # Quality detection
│   ├── Download/                     # Download client
│   │   ├── TidalDownloadClient.cs    # Main download client
│   │   ├── TidalStreamProcessor.cs   # DASH manifest handler
│   │   ├── TidalChunkDownloader.cs   # Chunk download manager
│   │   ├── TidalDecryptor.cs         # Stream decryption
│   │   └── TidalMetadataApplier.cs   # Metadata tagging
│   ├── Models/                       # Data models
│   │   ├── Authentication/           # Auth models
│   │   ├── API/                      # API DTOs
│   │   ├── Stream/                   # Streaming models
│   │   └── Lidarr/                   # Integration models
│   ├── Services/                     # Business logic
│   │   ├── TidalSearchService.cs     # Search orchestration
│   │   ├── TidalMetadataService.cs   # Metadata retrieval
│   │   ├── TidalLyricsService.cs     # Lyrics support
│   │   └── TidalCoverArtService.cs   # Cover art handling
│   ├── Configuration/                # Settings & constants
│   │   ├── TidalConstants.cs         # API endpoints, keys
│   │   ├── TidalarrSettings.cs   # Indexer configuration
│   │   └── TidalarrSettings.cs  # Download configuration
│   ├── Exceptions/                   # Custom exceptions
│   │   ├── TidalApiException.cs
│   │   ├── TidalAuthException.cs
│   │   └── TidalStreamException.cs
│   └── TidalarrModule.cs             # Plugin registration
├── TidalCLI/                         # Standalone CLI for testing
│   ├── Program.cs                    # CLI entry point
│   ├── Commands/                     # CLI commands
│   └── Utilities/                    # CLI helpers
├── tests/                            # Comprehensive test suite
│   ├── Unit/                         # Unit tests
│   ├── Integration/                  # Integration tests
│   └── Security/                     # Security tests
├── docs/                             # Documentation
├── scripts/                          # Build & deployment
├── ext/                              # External dependencies
└── plugin.json                       # Plugin manifest
```

---

## 2. Core Components Implementation

### 2.1 Authentication System

#### OAuth 2.0 PKCE Implementation

```csharp
// PKCEGenerator.cs
public class PKCEGenerator
{
    public (string codeVerifier, string codeChallenge) GeneratePKCEPair()
    {
        // Generate 128-character random code verifier
        var codeVerifier = GenerateCodeVerifier(128);
        
        // Create SHA256 hash and base64url encode for challenge
        using var sha256 = SHA256.Create();
        var challengeBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(codeVerifier));
        var codeChallenge = Base64UrlEncode(challengeBytes);
        
        return (codeVerifier, codeChallenge);
    }
}

// TidalOAuthManager.cs
public class TidalOAuthManager
{
    private const string CLIENT_ID = "6BDSRdpK9hqEBTgU";
    private const string CLIENT_SECRET = "xeuPmY7nbpZ9IIbLAcQ93shka1VNheUAqN6IcszjTG8=";
    private const string REDIRECT_URI = "https://tidal.com/android/login/auth";
    
    public string BuildAuthorizationUrl(string codeChallenge, string state)
    {
        var parameters = new Dictionary<string, string>
        {
            ["response_type"] = "code",
            ["client_id"] = CLIENT_ID,
            ["redirect_uri"] = REDIRECT_URI,
            ["scope"] = "r_usr+w_usr+w_sub",
            ["code_challenge"] = codeChallenge,
            ["code_challenge_method"] = "S256",
            ["state"] = state
        };
        
        return $"https://login.tidal.com/authorize?{BuildQueryString(parameters)}";
    }
    
    public async Task<TidalTokenResponse> ExchangeCodeForTokens(string authCode, string codeVerifier)
    {
        var request = new TokenRequest
        {
            grant_type = "authorization_code",
            code = authCode,
            client_id = CLIENT_ID,
            client_secret = CLIENT_SECRET,
            redirect_uri = REDIRECT_URI,
            code_verifier = codeVerifier
        };
        
        return await PostToTokenEndpoint(request);
    }
}
```

#### Session Management

```csharp
// TidalSession.cs
public class TidalSession
{
    public string AccessToken { get; set; }
    public string RefreshToken { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string SessionId { get; set; }
    public string CountryCode { get; set; }
    public string UserId { get; set; }
    
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt.AddMinutes(-5);
    
    public async Task RefreshIfNeeded(ITidalOAuthManager oauthManager)
    {
        if (IsExpired)
        {
            var newTokens = await oauthManager.RefreshTokens(RefreshToken);
            UpdateTokens(newTokens);
        }
    }
}
```

### 2.2 API Client Architecture

#### Main API Client

```csharp
// ITidalApiClient.cs
public interface ITidalApiClient
{
    Task<TidalSearchResponse> SearchAsync(string query, int limit, int offset);
    Task<TidalAlbum> GetAlbumAsync(string albumId);
    Task<TidalTrack> GetTrackAsync(string trackId);
    Task<TidalStreamInfo> GetStreamInfoAsync(string trackId, TidalQuality quality);
    Task<TidalLyrics> GetLyricsAsync(string trackId);
}

// TidalApiClient.cs
public class TidalApiClient : ITidalApiClient
{
    private readonly ITidalHttpClient _httpClient;
    private readonly ITidalAuthenticationService _authService;
    private readonly ITidalResponseCache _cache;
    private readonly ITidalRequestSigner _requestSigner;
    
    public async Task<TidalSearchResponse> SearchAsync(string query, int limit, int offset)
    {
        await _authService.EnsureAuthenticatedAsync();
        
        var cacheKey = $"search_{query}_{limit}_{offset}";
        if (_cache.TryGet(cacheKey, out TidalSearchResponse cached))
            return cached;
        
        var request = BuildSearchRequest(query, limit, offset);
        var signedRequest = await _requestSigner.SignRequestAsync(request);
        var response = await _httpClient.SendAsync<TidalSearchResponse>(signedRequest);
        
        _cache.Set(cacheKey, response, TimeSpan.FromMinutes(15));
        return response;
    }
}
```

#### Request Signing

```csharp
// TidalRequestSigner.cs
public class TidalRequestSigner : ITidalRequestSigner
{
    private readonly ITidalSession _session;
    
    public async Task<HttpRequestMessage> SignRequestAsync(HttpRequestMessage request)
    {
        await _session.RefreshIfNeeded();
        
        // Add authentication header
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _session.AccessToken);
        
        // Add required query parameters
        var uriBuilder = new UriBuilder(request.RequestUri);
        var query = HttpUtility.ParseQueryString(uriBuilder.Query);
        query["sessionId"] = _session.SessionId;
        query["countryCode"] = _session.CountryCode;
        uriBuilder.Query = query.ToString();
        request.RequestUri = uriBuilder.Uri;
        
        return request;
    }
}
```

### 2.3 Search Implementation

#### Indexer Integration

```csharp
// TidalIndexer.cs
public class TidalIndexer : HttpIndexerBase<TidalarrSettings>
{
    private readonly ITidalApiClient _apiClient;
    private readonly ITidalQualityDetector _qualityDetector;
    
    public override string Protocol => nameof(TidalarrDownloadProtocol);
    
    protected override async Task<IList<ReleaseInfo>> FetchReleases(IndexerRequest request)
    {
        var searchTerm = request.SearchCriteria.SearchTerm;
        var releases = new List<ReleaseInfo>();
        
        // Search with pagination (3 pages, 100 items each)
        for (int page = 0; page < 3; page++)
        {
            var response = await _apiClient.SearchAsync(searchTerm, 100, page * 100);
            
            // Process albums
            foreach (var album in response.albums.items)
            {
                var release = MapAlbumToRelease(album);
                releases.Add(release);
            }
            
            // Process tracks as singles
            foreach (var track in response.tracks.items)
            {
                var release = MapTrackToRelease(track);
                releases.Add(release);
            }
        }
        
        return releases;
    }
}
```

#### Quality Detection

```csharp
// TidalQualityDetector.cs
public class TidalQualityDetector : ITidalQualityDetector
{
    public List<TidalQuality> DetectAvailableQualities(TidalMediaMetadata metadata)
    {
        var qualities = new List<TidalQuality>();
        
        if (metadata.tags?.Contains("HIRES_LOSSLESS") == true)
        {
            qualities.Add(TidalQuality.HiRes);
            qualities.Add(TidalQuality.Lossless);
        }
        else if (metadata.tags?.Contains("LOSSLESS") == true)
        {
            qualities.Add(TidalQuality.Lossless);
        }
        
        // Always available
        qualities.Add(TidalQuality.High);
        qualities.Add(TidalQuality.Low);
        
        return qualities;
    }
}
```

### 2.4 Download System

#### Stream Processing

```csharp
// TidalStreamProcessor.cs
public class TidalStreamProcessor : ITidalStreamProcessor
{
    private const string MASTER_KEY_BASE64 = "UIlTTEMmmLfGowo/UC60x2H45W6MdGgTRfo/umg4754=";
    
    public async Task<TidalStreamData> ProcessStreamAsync(string trackId, TidalQuality quality)
    {
        // Get playback info
        var playbackInfo = await _apiClient.GetPlaybackInfoAsync(trackId, quality);
        
        // Decode manifest
        var manifestJson = Base64Decode(playbackInfo.manifest);
        var manifest = JsonSerializer.Deserialize<TidalManifest>(manifestJson);
        
        // Extract URLs based on format
        List<string> chunkUrls;
        if (manifest.mimeType == "application/dash+xml")
        {
            chunkUrls = ParseDashManifest(manifest);
        }
        else if (manifest.mimeType == "application/vnd.tidal.bts")
        {
            chunkUrls = ParseBtsManifest(manifest);
        }
        
        return new TidalStreamData
        {
            ChunkUrls = chunkUrls,
            IsEncrypted = playbackInfo.encryptionType != "NONE",
            SecurityToken = playbackInfo.securityToken,
            SecurityType = playbackInfo.securityType
        };
    }
}

// TidalChunkDownloader.cs
public class TidalChunkDownloader : ITidalChunkDownloader
{
    public async Task<byte[]> DownloadAndAssembleAsync(List<string> chunkUrls)
    {
        using var memoryStream = new MemoryStream();
        
        // Download chunks with concurrency control
        var semaphore = new SemaphoreSlim(4); // Max 4 concurrent downloads
        var tasks = chunkUrls.Select(async url =>
        {
            await semaphore.WaitAsync();
            try
            {
                return await DownloadChunkAsync(url);
            }
            finally
            {
                semaphore.Release();
            }
        });
        
        var chunks = await Task.WhenAll(tasks);
        
        // Assemble in order
        foreach (var chunk in chunks)
        {
            await memoryStream.WriteAsync(chunk, 0, chunk.Length);
        }
        
        return memoryStream.ToArray();
    }
}
```

#### Stream Decryption

```csharp
// TidalDecryptor.cs
public class TidalDecryptor : ITidalDecryptor
{
    private readonly byte[] _masterKey;
    
    public TidalDecryptor()
    {
        _masterKey = Convert.FromBase64String(MASTER_KEY_BASE64);
    }
    
    public byte[] DecryptStream(byte[] encryptedData, string securityToken)
    {
        // Decrypt security token to get stream key/nonce
        var tokenBytes = Convert.FromBase64String(securityToken);
        var (streamKey, nonce) = DecryptSecurityToken(tokenBytes);
        
        // Decrypt audio stream
        using var aes = new AesCryptoServiceProvider
        {
            Key = streamKey,
            IV = nonce,
            Mode = CipherMode.CBC,
            Padding = PaddingMode.PKCS7
        };
        
        using var decryptor = aes.CreateDecryptor();
        return decryptor.TransformFinalBlock(encryptedData, 0, encryptedData.Length);
    }
}
```

### 2.5 Metadata Services

```csharp
// TidalMetadataService.cs
public class TidalMetadataService : ITidalMetadataService
{
    public async Task<TidalCompleteMetadata> GetCompleteMetadataAsync(string trackId)
    {
        // Fetch track and album data in parallel
        var trackTask = _apiClient.GetTrackAsync(trackId);
        var albumTask = _apiClient.GetAlbumAsync(albumId);
        var lyricsTask = _lyricsService.GetLyricsAsync(trackId);
        
        await Task.WhenAll(trackTask, albumTask, lyricsTask);
        
        return new TidalCompleteMetadata
        {
            Track = trackTask.Result,
            Album = albumTask.Result,
            Lyrics = lyricsTask.Result,
            CoverArtUrl = BuildCoverArtUrl(albumTask.Result.cover)
        };
    }
    
    private string BuildCoverArtUrl(string coverHash, int resolution = 1280)
    {
        var formattedHash = coverHash.Replace("-", "/");
        return $"https://resources.tidal.com/images/{formattedHash}/{resolution}x{resolution}.jpg";
    }
}
```

---

## 3. Configuration & Settings

### 3.1 Plugin Settings

```csharp
// TidalarrSettings.cs
public class TidalarrSettings : IIndexerSettings
{
    private static readonly TidalarrSettingsValidator Validator = new();
    
    [FieldDefinition(1, Label = "Authentication Method", Type = FieldType.Select, 
                     SelectOptions = new[] { "OAuth Browser", "Token Import" })]
    public string AuthMethod { get; set; } = "OAuth Browser";
    
    [FieldDefinition(2, Label = "Access Token", Type = FieldType.Password, 
                     HelpText = "For manual token import")]
    public string AccessToken { get; set; }
    
    [FieldDefinition(3, Label = "Refresh Token", Type = FieldType.Password)]
    public string RefreshToken { get; set; }
    
    [FieldDefinition(4, Label = "Preferred Quality", Type = FieldType.Select,
                     SelectOptions = new[] { "HiRes", "Lossless", "High", "Low" })]
    public string PreferredQuality { get; set; } = "Lossless";
    
    [FieldDefinition(5, Label = "Enable Lyrics", Type = FieldType.Checkbox)]
    public bool EnableLyrics { get; set; } = true;
    
    [FieldDefinition(6, Label = "Cache Duration (minutes)", Type = FieldType.Number)]
    public int CacheDuration { get; set; } = 15;
}
```

### 3.2 Constants

```csharp
// TidalConstants.cs
public static class TidalConstants
{
    // API Endpoints
    public const string API_BASE_V1 = "https://api.tidal.com/v1/";
    public const string API_BASE_V2 = "https://api.tidal.com/v2/";
    public const string AUTH_BASE = "https://auth.tidal.com/v1/";
    public const string LOGIN_BASE = "https://login.tidal.com/";
    
    // Client Credentials
    public const string CLIENT_ID_PKCE = "6BDSRdpK9hqEBTgU";
    public const string CLIENT_SECRET_PKCE = "xeuPmY7nbpZ9IIbLAcQ93shka1VNheUAqN6IcszjTG8=";
    public const string REDIRECT_URI = "https://tidal.com/android/login/auth";
    
    // Encryption
    public const string MASTER_KEY = "UIlTTEMmmLfGowo/UC60x2H45W6MdGgTRfo/umg4754=";
    
    // Quality Mappings
    public static readonly Dictionary<TidalQuality, string> QualityParameters = new()
    {
        [TidalQuality.Low] = "LOW",
        [TidalQuality.High] = "HIGH",
        [TidalQuality.Lossless] = "LOSSLESS",
        [TidalQuality.HiRes] = "HI_RES_LOSSLESS"
    };
}
```

---

## 4. Testing Strategy

### 4.1 Unit Tests

```csharp
// Authentication Tests
[TestFixture]
public class TidalAuthenticationTests
{
    [Test]
    public void PKCEGenerator_GeneratesValidChallenge()
    {
        var generator = new PKCEGenerator();
        var (verifier, challenge) = generator.GeneratePKCEPair();
        
        Assert.That(verifier.Length, Is.EqualTo(128));
        Assert.That(challenge, Does.Match(@"^[A-Za-z0-9_-]+$"));
    }
    
    [Test]
    public async Task OAuthManager_RefreshesExpiredToken()
    {
        // Test automatic token refresh
    }
}

// API Client Tests
[TestFixture]
public class TidalApiClientTests
{
    [Test]
    public async Task SearchAsync_ReturnsCachedResults()
    {
        // Test cache functionality
    }
    
    [Test]
    public async Task GetStreamInfo_HandlesAllQualities()
    {
        // Test quality handling
    }
}
```

### 4.2 Integration Tests

```csharp
// End-to-End Tests
[TestFixture]
public class TidalIntegrationTests
{
    [Test]
    public async Task CompleteDownloadFlow_WorksCorrectly()
    {
        // 1. Authenticate
        // 2. Search for content
        // 3. Get stream info
        // 4. Download and decrypt
        // 5. Apply metadata
    }
}
```

### 4.3 Security Tests

```csharp
// Security Validation
[TestFixture]
public class TidalSecurityTests
{
    [Test]
    public void Credentials_NotHardcodedInProduction()
    {
        // Verify environment variable usage
    }
    
    [Test]
    public void Decryption_HandlesInvalidTokens()
    {
        // Test error handling for decryption
    }
}
```

---

## 5. Build & Deployment

### 5.1 Build Configuration

```xml
<!-- Tidalarr.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net6.0</TargetFramework>
    <AssemblyName>Tidalarr</AssemblyName>
    <RootNamespace>Tidalarr</RootNamespace>
  </PropertyGroup>
  
  <ItemGroup>
    <PackageReference Include="FluentValidation" Version="11.5.1" />
    <PackageReference Include="System.Text.Json" Version="7.0.0" />
    <PackageReference Include="Microsoft.Extensions.Caching.Memory" Version="7.0.0" />
  </ItemGroup>
</Project>
```

### 5.2 Plugin Manifest

```json
{
  "name": "Tidalarr",
  "version": "1.0.0",
  "description": "Tidal integration for Lidarr",
  "author": "RicherTunes",
  "minimumVersion": "1.0.0.0",
  "assembly": "Tidalarr.dll",
  "protocols": ["TidalarrDownloadProtocol"],
  "indexers": ["TidalIndexer"],
  "downloadClients": ["TidalDownloadClient"]
}
```

### 5.3 Build Scripts

```powershell
# build.ps1
param(
    [string]$Configuration = "Release",
    [switch]$Deploy
)

# Build plugin
dotnet build src/Tidalarr.csproj -c $Configuration

# Run tests
dotnet test tests/Tidalarr.Tests.csproj

# Package plugin
if ($Deploy) {
    $output = "dist/Tidalarr"
    Copy-Item "src/bin/$Configuration/net6.0/*" $output -Recurse
    Copy-Item "plugin.json" $output
    
    # Create deployment package
    Compress-Archive -Path $output -DestinationPath "dist/Tidalarr.zip"
}
```

---

## 6. Implementation Phases

### Phase 1: Foundation (Week 1-2)
1. Set up project structure
2. Implement OAuth 2.0 PKCE authentication
3. Create basic API client with session management
4. Build core models and DTOs
5. Implement configuration system

### Phase 2: Core Functionality (Week 3-4)
1. Implement search indexer
2. Build quality detection system
3. Create stream processor for DASH manifests
4. Implement chunk downloader
5. Add decryption support

### Phase 3: Integration (Week 5-6)
1. Complete Lidarr integration interfaces
2. Implement download client
3. Add metadata services
4. Build lyrics support
5. Create CLI for testing

### Phase 4: Polish & Testing (Week 7-8)
1. Comprehensive unit tests
2. Integration testing
3. Performance optimization
4. Error handling improvements
5. Documentation

### Phase 5: Advanced Features (Week 9-10)
1. ML-based search optimization (like Qobuzarr)
2. Advanced caching strategies
3. Batch download support
4. Extended metadata (credits, etc.)
5. Release preparation

---

## 7. Key Implementation Notes

### Critical Requirements
1. **Never hardcode credentials in production** - Use environment variables
2. **Implement proper token refresh** - Prevent auth failures
3. **Handle DASH manifest formats correctly** - Both MPD and BTS
4. **Preserve exact client IDs** - Required for API access
5. **Implement proper rate limiting** - Avoid API bans

### Performance Considerations
1. **Parallel chunk downloads** - Max 4 concurrent
2. **Response caching** - 15-minute default TTL
3. **Lazy authentication** - Only authenticate when needed
4. **Efficient manifest parsing** - Stream processing
5. **Memory-efficient downloads** - Stream to disk for large files

### Security Considerations
1. **Secure credential storage** - Encrypted settings
2. **Token rotation** - Regular refresh
3. **Input validation** - All user inputs sanitized
4. **Secure decryption** - Proper key management
5. **No logging of sensitive data** - Tokens, passwords

---

## 8. Migration from TidalSharp

### Components to Recreate
1. **OAuth PKCE Flow** - Complete implementation
2. **API Client** - All endpoints and parameters
3. **Manifest Parsing** - DASH and BTS support
4. **Stream Decryption** - AES-CBC with master key
5. **Metadata Application** - Complete tag support

### Improvements Over TidalSharp
1. **Better error handling** - Comprehensive exception hierarchy
2. **Caching system** - Reduce API calls
3. **Modular architecture** - Easier maintenance
4. **Test coverage** - Full unit and integration tests
5. **Configuration flexibility** - Environment variables and settings

---

## 9. Maintenance & Support

### Monitoring
1. API response times
2. Download success rates
3. Authentication failures
4. Cache hit ratios
5. Error frequencies

### Regular Updates
1. API endpoint changes
2. Client credential rotation
3. New quality tiers
4. Protocol updates
5. Security patches

### Documentation
1. User guide for setup
2. API documentation
3. Troubleshooting guide
4. Developer documentation
5. Release notes

---

## Conclusion

This architectural plan provides a complete blueprint for implementing Tidalarr as a robust, maintainable Tidal integration for Lidarr. By following Qobuzarr's proven architecture while implementing Tidal's specific requirements, we ensure a production-ready solution that is both reliable and performant.

The modular design allows for easy maintenance and future enhancements, while the comprehensive testing strategy ensures stability. The phased implementation approach allows for iterative development with regular validation milestones.

Key success factors:
- Strict adherence to Lidarr's plugin interfaces
- Robust OAuth 2.0 implementation with automatic refresh
- Efficient DASH manifest processing
- Comprehensive error handling
- Performance optimization through caching
- Security-first design principles

With this plan, Tidalarr will provide a seamless Tidal experience for Lidarr users while maintaining the high standards set by the Qobuzarr implementation.

