# Tidalarr Implementation Plan v2
## Pragmatic Approach: Port TidalSharp into Qobuzarr Architecture

---

## Executive Summary

This revised plan focuses on creating a working Tidal integration by porting TidalSharp's proven functionality directly into Qobuzarr's plugin architecture. We prioritize functionality over architectural purity, accepting technical debt for immediate working results.

**Core Strategy:** Use Qobuzarr's skeleton, TidalSharp's guts.

---

## 1. Architecture Overview

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
│   │   ├── TidalarrSettings.cs  # Lidarr settings UI
│   │   └── TidalarrSettings.cs # Lidarr settings UI
│   │
│   ├── Services/                    # Adapter layer
│   │   ├── TidalSessionAdapter.cs   # Wraps TidalSharp's Session
│   │   ├── TidalApiAdapter.cs       # Wraps TidalSharp's API
│   │   └── TidalDownloadAdapter.cs  # Wraps download logic
│   │
│   └── TidalarrModule.cs            # Plugin registration
│
├── docs/
│   ├── TECH-DEBT-INVENTORY.md       # Explicit tech debt tracking
│   └── PORTING-NOTES.md             # Specific changes made during port
│
└── plugin.json                       # Lidarr plugin manifest
```

---

## 2. Direct Port Components (From TidalSharp)

### 2.1 Keep Exactly As-Is

#### Authentication (Session.cs)
```csharp
// Direct port - DO NOT MODIFY LOGIC
public class TidalSession
{
    // Keep ALL existing OAuth flow exactly
    private const string CLIENT_ID = "zU4XHVVkc2tDPo4t";
    private const string CLIENT_SECRET = "VJKhDFqJPqvsPVNBV6ukXTJmwlvbttP7wlMlrc72se4=";
    private const string CLIENT_ID_PKCE = "6BDSRdpK9hqEBTgU";
    private const string CLIENT_SECRET_PKCE = "xeuPmY7nbpZ9IIbLAcQ93shka1VNheUAqN6IcszjTG8=";
    
    // Port Login, GetOAuthLoginUrl, LoginOAuth2 methods EXACTLY
    // Keep the same redirect URI: "https://tidal.com/android/login/auth"
    // Keep refresh token logic unchanged
}
```

#### API Communication (API.cs)
```csharp
// Direct port - PRESERVE ALL ENDPOINTS
public class TidalAPI
{
    // Keep API v1 endpoints - DO NOT upgrade to v2
    private const string API_URL = "https://api.tidal.com/v1/";
    
    // Port these methods EXACTLY:
    // - GetRequest (with sessionId, countryCode parameters)
    // - GetAlbum, GetTrack, GetArtist
    // - GetAlbumTracks, GetArtistAlbums
    // - GetPlaybackInfoPostPaywall (critical for downloads)
    // - Search (keep 3 pages, 100 items logic)
}
```

#### Stream Processing (Decryption.cs & Manifest parsing)
```csharp
// Direct port - CRITICAL FUNCTIONALITY
public class TidalDecryption
{
    // MUST keep master key exactly
    private static readonly byte[] MASTER_KEY = 
        Convert.FromBase64String("UIlTTEMmmLfGowo/UC60x2H45W6MdGgTRfo/umg4754=");
    
    // Port DecryptSecurityToken method EXACTLY
    // Port DecryptStream method EXACTLY
}

// From download logic - keep manifest parsing
public class ManifestParser
{
    // Port ParseMPD method EXACTLY (working DASH support)
    // Port ParseBTS method AS-IS (even if incomplete)
    // Keep chunk URL extraction logic EXACTLY
    // PRESERVE CHUNK ORDERING - critical!
}
```

### 2.2 Critical Implementation Details to Preserve

1. **Search Pagination**: Keep the 3-page, 100-item limit
2. **Quality Detection**: Keep tag-based detection ("HIRES_LOSSLESS", "LOSSLESS")
3. **Chunk Download Order**: Sequential download is MANDATORY
4. **Request Parameters**: Always include sessionId and countryCode
5. **Token Refresh**: Keep the exact refresh logic and timing
6. **Download Flow**:
   - GetPlaybackInfoPostPaywall with exact parameters
   - Base64 decode manifest
   - Parse MPD/BTS for URLs
   - Download chunks IN ORDER
   - Concatenate chunks
   - Decrypt if needed

---

## 3. Adapter Layer (New Code)

### 3.1 Session Adapter
```csharp
// Wraps TidalSharp's Session for Qobuzarr-style services
public class TidalSessionAdapter : ITidalAuthenticationService
{
    private readonly TidalSession _tidalSession; // TidalSharp's Session
    
    public TidalSessionAdapter()
    {
        _tidalSession = new TidalSession();
    }
    
    public async Task<bool> AuthenticateAsync(string username, string password)
    {
        // Call _tidalSession.Login() directly
        return await _tidalSession.Login(username, password);
    }
    
    public async Task<bool> AuthenticateOAuthAsync(string authCode, string codeVerifier)
    {
        // Call _tidalSession.LoginOAuth2() directly
        return await _tidalSession.LoginOAuth2(authCode, codeVerifier);
    }
}
```

### 3.2 API Adapter
```csharp
// Thin wrapper around TidalSharp's API
public class TidalApiAdapter : ITidalApiClient
{
    private readonly TidalAPI _tidalApi; // TidalSharp's API
    private readonly TidalSession _session;
    
    public async Task<SearchResult> SearchAsync(string query)
    {
        // Call _tidalApi.Search() directly
        var tidalResults = await _tidalApi.Search(query, 100, 0);
        
        // Minimal transformation to our models if needed
        return MapToSearchResult(tidalResults);
    }
}
```

---

## 4. Lidarr Integration Layer

### 4.1 Indexer Implementation
```csharp
public class TidalIndexer : HttpIndexerBase<TidalarrSettings>
{
    private readonly TidalApiAdapter _apiAdapter;
    
    protected override async Task<IList<ReleaseInfo>> FetchReleases(IndexerRequest request)
    {
        // Use adapter to call TidalSharp's search
        var results = await _apiAdapter.SearchAsync(request.SearchCriteria.SearchTerm);
        
        // Map to Lidarr's ReleaseInfo format
        return MapToReleases(results);
    }
    
    public override string Protocol => nameof(TidalarrDownloadProtocol);
}
```

### 4.2 Download Client
```csharp
public class TidalDownloadClient : DownloadClientBase<TidalarrSettings>
{
    private readonly TidalDownloadAdapter _downloadAdapter;
    
    public override async Task<byte[]> DownloadAsync(RemoteAlbum remoteAlbum)
    {
        // Use adapter which calls TidalSharp's download logic
        foreach (var track in remoteAlbum.Tracks)
        {
            var trackData = await _downloadAdapter.DownloadTrackAsync(track.Id);
            // Save track using TidalSharp's exact logic
        }
    }
}
```

---

## 5. Settings (Qobuzarr Style)

```csharp
public class TidalarrSettings : IIndexerSettings
{
    [FieldDefinition(1, Label = "Username/Email", Type = FieldType.Textbox)]
    public string Username { get; set; }
    
    [FieldDefinition(2, Label = "Password", Type = FieldType.Password)]
    public string Password { get; set; }
    
    [FieldDefinition(3, Label = "Use OAuth", Type = FieldType.Checkbox, 
                     HelpText = "Use browser OAuth instead of username/password")]
    public bool UseOAuth { get; set; }
    
    [FieldDefinition(4, Label = "Preferred Quality", Type = FieldType.Select,
                     SelectOptions = new[] { "Master", "Lossless", "High", "Low" })]
    public string Quality { get; set; } = "Lossless";
}
```

---

## 6. Implementation Phases

### Phase 1: Direct Port (Week 1)
1. Copy ALL TidalSharp source files into TidalCore/
2. Fix namespace conflicts only
3. Add required NuGet packages
4. Verify compilation
5. DO NOT refactor or improve code

### Phase 2: Adapter Layer (Week 2)
1. Create minimal adapter classes
2. Wrap TidalSharp methods with no logic changes
3. Test that adapters call through correctly
4. No optimizations or improvements

### Phase 3: Lidarr Integration (Week 3)
1. Implement TidalIndexer using adapters
2. Implement TidalDownloadClient using adapters
3. Create settings classes
4. Test with Lidarr instance

### Phase 4: Testing & Stabilization (Week 4)
1. End-to-end testing with real Tidal account
2. Fix only breaking bugs
3. Document any workarounds needed
4. Package for deployment

---

## 7. Tech Debt Inventory

Create `TECH-DEBT-INVENTORY.md`:

```markdown
# Technical Debt Inventory

## Inherited from TidalSharp
1. **API v1 Dependency**: Using deprecated API that may stop working
2. **Hardcoded Credentials**: Client IDs and secrets in code
3. **Incomplete BTS Support**: BTS manifest parsing not fully implemented
4. **Limited Error Handling**: Basic retry logic only
5. **No Unit Tests**: Original code lacks test coverage
6. **Coupled Code**: API, Session, Download logic tightly coupled

## From Our Implementation
1. **Adapter Pattern Overhead**: Extra layer of abstraction
2. **No Caching**: Direct API calls without response caching
3. **No Rate Limiting**: Beyond TidalSharp's basic retry
4. **Synchronous Operations**: Some async patterns not fully utilized

## Future Improvements (Post-V1)
1. Migrate to API v2 when stable
2. Extract credentials to secure storage
3. Complete BTS manifest support
4. Comprehensive error handling
5. Add unit test coverage
6. Implement response caching
```

---

## 8. Critical Success Factors

### What MUST Work
1. OAuth login flow (browser-based)
2. Search returning results
3. Quality detection from tags
4. MPD manifest parsing
5. Sequential chunk downloading
6. Track decryption (if protected)
7. Metadata application

### What Can Be Deferred
1. BTS manifest support (MPD is enough)
2. Lyrics integration
3. Cover art optimization
4. Advanced error handling
5. Performance optimizations
6. Caching layer
7. ML search optimization

---

## 9. Testing Checklist

### Minimal Viable Testing
```
□ Can authenticate with username/password
□ Can authenticate with OAuth browser flow
□ Can search for an artist
□ Can search for an album
□ Can detect available qualities
□ Can download a standard quality track
□ Can download a lossless track
□ Can download a Hi-Res track (if available)
□ Downloaded files play correctly
□ Metadata is applied to files
```

### Known Issues to Document
- BTS streams may fail (use MPD only)
- Rate limiting may occur (manual retry)
- Some regions may have different quality availability

---

## 10. Build & Deployment

### Minimal Build Script
```powershell
# build.ps1
param([string]$Configuration = "Release")

# Build
dotnet build src/Tidalarr.csproj -c $Configuration

# Copy to Lidarr plugins folder for testing
$lidarrPlugins = "$env:APPDATA\Lidarr\Plugins"
Copy-Item "src\bin\$Configuration\net6.0\Tidalarr.dll" $lidarrPlugins

Write-Host "Tidalarr built and deployed to $lidarrPlugins"
Write-Host "Restart Lidarr to load plugin"
```

---

## Key Principles

1. **Don't Improve - Port**: Resist the urge to fix or improve TidalSharp code
2. **Test with Real Account**: Use actual Tidal credentials for all testing
3. **Document Everything**: Every workaround, every known issue
4. **Minimal Viable Product**: Get it working, then improve
5. **Preserve Working Logic**: If it works in TidalSharp, keep it exactly

---

## Success Metrics

**V1 is successful if:**
- Users can search Tidal through Lidarr
- Users can download tracks in their preferred quality
- Downloads complete without corruption
- Basic metadata is applied

**V1 does NOT need:**
- Perfect error handling
- Optimal performance
- Complete format support
- Beautiful code

This pragmatic approach prioritizes a working integration over architectural perfection, accepting technical debt as a strategic decision to deliver functionality quickly.

