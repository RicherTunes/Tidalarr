> **Note:** This document is historical and may not reflect current architecture. It was written during the feasibility analysis phase. See CLAUDE.md for current guidance.

# Tidalarr Validated Implementation Plan
## Based on TidalSharp Analysis and Shared Library Integration

---

## Executive Summary

After analyzing the existing TidalSharp implementation, the plan is **validated and feasible** with some important refinements. TidalSharp provides a solid foundation for streaming/download functionality that can be successfully integrated with the Lidarr.Plugin.Common shared library.

**Key Finding**: TidalSharp's core streaming logic is self-contained and portable, while the Lidarr integration points are well-defined. This makes the shared library approach even more valuable.

---

## 1. Implementation Feasibility Analysis

### ✅ **CONFIRMED WORKING:**

**Authentication System:**
- OAuth2 PKCE flow with code challenge generation
- Token refresh mechanism with retry logic  
- Session persistence to JSON files
- Rate limiting and error recovery

**Search/Indexer:**
- Tidal API v1 integration (tracks, albums, artists)
- Quality detection from API responses
- Pagination support (limit 1000 items)
- Result mapping to Lidarr ReleaseInfo

**Download/Streaming:**
- DASH manifest parsing (MPD format) ✅ Working
- BTS manifest parsing (partial implementation)
- Chunk-based sequential download
- Stream decryption (AES, though noted as "untested")
- Metadata application with TagLibSharp

### ⚠️ **POTENTIAL CHALLENGES:**

**Lidarr Coupling:**
- Heavy dependency on `IHttpClient` from Lidarr
- Uses Lidarr's logging and configuration systems
- Inherits from Lidarr base classes

**Stream Processing:**
- BTS manifest support is incomplete
- Encryption handling marked as "untested"
- FFMPEG dependency for some operations

---

## 2. Refined Architecture Strategy

### 2.1 Three-Layer Approach

```
┌─────────────────────────────────────────────┐
│           Lidarr Integration Layer          │
│  (TidalIndexer, TidalDownloadClient)        │
│                                             │
│  Uses: Lidarr.Plugin.Common                 │
└─────────────────────────────────────────────┘
                        │
┌─────────────────────────────────────────────┐
│           Adapter/Bridge Layer              │
│  (TidalApiAdapter, TidalAuthAdapter)        │
│                                             │
│  Bridges: Shared Library ↔ TidalSharp       │
└─────────────────────────────────────────────┘
                        │
┌─────────────────────────────────────────────┐
│           TidalSharp Core Layer             │
│  (API, Session, Downloader, StreamManifest) │
│                                             │
│  Direct Port: Minimal modifications         │
└─────────────────────────────────────────────┘
```

### 2.2 Component Mapping

| TidalSharp Component | Integration Strategy | Lines of Code |
|---------------------|---------------------|---------------|
| **API.cs** | Port with HTTP adapter | ~200 → ~150 |
| **Session.cs** | Port with auth adapter | ~300 → ~200 |
| **Downloader.cs** | Port with minimal changes | ~400 → ~350 |
| **StreamManifest.cs** | Port directly (self-contained) | ~87 → ~87 |
| **Decryption.cs** | Port directly (self-contained) | ~50 → ~50 |
| **Data Models** | Port directly | ~200 → ~200 |
| **Lidarr Integration** | Replace with shared library | ~500 → ~150 |

**Total Reduction: ~1,237 lines → ~1,187 lines** (only 4% reduction in code, but **massive** reduction in complexity and risk)

---

## 3. Detailed Implementation Plan

### 3.1 Phase 1: Foundation Setup (Week 1)

**Day 1-2: Project Structure**
```
Tidalarr/
├── src/
│   ├── Tidalarr.csproj                    # Reference Lidarr.Plugin.Common
│   │
│   ├── TidalSharp/                        # Direct port from existing
│   │   ├── API.cs                         # Modified for adapter pattern
│   │   ├── Session.cs                     # Modified for adapter pattern  
│   │   ├── Downloader.cs                  # Minimal modifications
│   │   ├── Downloading/                   # Direct port
│   │   │   ├── StreamManifest.cs
│   │   │   ├── MPD.cs
│   │   │   └── DashInfo.cs
│   │   ├── Data/                          # Direct port
│   │   └── Exceptions/                    # Direct port
│   │
│   ├── Adapters/                          # Bridge layer
│   │   ├── TidalHttpClientAdapter.cs      # IHttpClient → StreamingApiRequestBuilder
│   │   ├── TidalAuthAdapter.cs            # Session → BaseStreamingAuthenticationService
│   │   └── TidalLoggingAdapter.cs         # Lidarr logging → shared library
│   │
│   ├── Services/                          # Tidal-specific business logic
│   │   ├── TidalApiService.cs             # Uses adapted API class
│   │   ├── TidalAuthenticationService.cs  # Extends BaseStreamingAuthenticationService
│   │   └── TidalStreamingService.cs       # Uses adapted Downloader
│   │
│   └── Integration/                       # Lidarr plugin implementation
│       ├── TidalIndexer.cs                # Uses shared library + services
│       ├── TidalDownloadClient.cs         # Uses shared library + services
│       ├── TidalSettings.cs               # Extends BaseStreamingSettings
│       └── TidalModule.cs                 # Plugin registration
```

**Day 3-5: Adapter Layer**
```csharp
// TidalHttpClientAdapter.cs
public class TidalHttpClientAdapter : IHttpClient
{
    private readonly StreamingApiRequestBuilder _builder;
    
    public HttpRequestBuilder BuildRequest(string url)
    {
        return new HttpRequestBuilderAdapter(_builder.BaseUrl(url));
    }
    
    public async Task<HttpResponse> ProcessRequestAsync(HttpRequest request)
    {
        var sharedRequest = ConvertToSharedLibraryRequest(request);
        var sharedResponse = await _builder.ExecuteWithRetryAsync(sharedRequest);
        return ConvertToLidarrHttpResponse(sharedResponse);
    }
}

// TidalAuthAdapter.cs  
public class TidalAuthenticationService : BaseStreamingAuthenticationService<TidalSession, TidalCredentials>
{
    private readonly Session _tidalSession; // TidalSharp Session
    
    protected override async Task<TidalSession> CreateSessionAsync(TidalCredentials credentials)
    {
        // Use TidalSharp's OAuth flow
        var oauthData = await _tidalSession.GetOAuthDataFromRedirect(credentials.RedirectUrl);
        return MapToSharedSession(oauthData);
    }
}
```

### 3.2 Phase 2: Core Services (Week 2)

**TidalSharp Port Strategy:**
1. **Copy files directly** from TidalSharp with minimal changes
2. **Replace IHttpClient usage** with adapter
3. **Keep all core logic intact** (OAuth, manifest parsing, decryption)
4. **Maintain API endpoints and parameters** exactly

```csharp
// Modified TidalSharp API.cs
public class API
{
    internal API(IHttpClient client, Session session) // Keep interface the same
    {
        _httpClient = client; // Will be our adapter
        _session = session;
    }
    
    // All existing methods remain unchanged
    public async Task<JObject> GetTrack(string id, CancellationToken token = default)
        => await Call(HttpMethod.Get, $"tracks/{id}", token: token);
    
    // Keep exact same Call method implementation
    internal async Task<JObject> Call(HttpMethod method, string path, ...)
    {
        // Existing TidalSharp logic - no changes needed
    }
}
```

### 3.3 Phase 3: Integration Layer (Week 3)

**Use Shared Library Patterns:**
```csharp
// TidalIndexer.cs - Leverage shared library
public class TidalIndexer : HttpIndexerBase<TidalSettings>
{
    private readonly TidalApiService _apiService;
    private readonly QualityMapper _qualityMapper;
    private readonly IStreamingResponseCache _cache;
    
    protected override async Task<IList<ReleaseInfo>> FetchReleases(IndexerRequest request)
    {
        // Use shared caching
        var cacheKey = $"tidal_search_{request.SearchCriteria.SearchTerm}";
        if (_cache.TryGet(cacheKey, out IList<ReleaseInfo> cached))
            return cached;
            
        // Use TidalSharp via service adapter
        var results = await _apiService.SearchAsync(request.SearchCriteria.SearchTerm);
        
        // Use shared quality mapping
        var releases = results.Select(r => new ReleaseInfo
        {
            Title = r.Title,
            Artist = string.Join(", ", r.Artists),
            Quality = _qualityMapper.MapToLidarrQuality(r.AudioQuality),
            // ... other mappings
        }).ToList();
        
        _cache.Set(cacheKey, releases, TimeSpan.FromMinutes(Settings.CacheDuration));
        return releases;
    }
}

// TidalDownloadClient.cs
public class TidalDownloadClient : DownloadClientBase<TidalSettings>
{
    private readonly TidalStreamingService _streamingService;
    
    public override async Task<string> Download(RemoteAlbum remoteAlbum, IIndexer indexer)
    {
        // Extract album ID from Lidarr release
        var albumId = ExtractAlbumId(remoteAlbum.Release.DownloadUrl);
        
        // Use TidalSharp for actual download via service
        var downloadResult = await _streamingService.DownloadAlbumAsync(
            albumId, 
            Settings.PreferredQuality,
            Settings.OutputPath);
            
        return downloadResult.JobId;
    }
}
```

### 3.4 Phase 4: Testing & Validation (Week 4)

**Critical Test Scenarios:**
1. **OAuth Flow**: Complete authentication with real Tidal account
2. **Search Functionality**: Multiple queries with quality detection
3. **Manifest Parsing**: Both MPD and BTS formats (if available)  
4. **Stream Download**: Sequential chunk download and assembly
5. **Metadata Application**: Cover art and tag writing with TagLibSharp
6. **Error Handling**: Token refresh, rate limiting, network failures

---

## 4. Risk Mitigation Strategies

### 4.1 High-Risk Areas

**Stream Decryption:**
- **Risk**: Marked as "untested" in TidalSharp
- **Mitigation**: Test extensively with encrypted content, implement fallback to unencrypted
- **Fallback**: Focus on MPD manifest (more reliable) over BTS

**BTS Manifest Support:**
- **Risk**: Incomplete implementation in TidalSharp
- **Mitigation**: Implement MPD-first strategy, treat BTS as enhancement
- **Testing**: Validate with real Tidal content

**Rate Limiting:**
- **Risk**: Tidal API changes or stricter limits
- **Mitigation**: Use shared library's retry logic, implement backoff strategies
- **Monitoring**: Track API response codes and adjust accordingly

### 4.2 Dependency Management

**External Dependencies:**
```xml
<PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
<PackageReference Include="TagLibSharp" Version="2.3.0" />
<PackageReference Include="Lidarr.Plugin.Common" Version="1.0.0" />
```

**Internal Dependencies:**
- Keep TidalSharp as internal source code (not external package)
- Use adapter pattern to isolate Lidarr-specific interfaces
- Shared library handles all Lidarr integration complexity

---

## 5. Success Metrics & Validation

### 5.1 Must Work (MVP)
- [ ] OAuth authentication with browser redirect
- [ ] Search returns accurate results with quality detection  
- [ ] Can download at least one track successfully
- [ ] Metadata is applied correctly
- [ ] Integration with Lidarr indexer/download client interfaces

### 5.2 Should Work (V1.0)
- [ ] Multiple quality tiers (Low, High, Lossless, HiRes)
- [ ] Album download with track organization
- [ ] Cover art download and application
- [ ] Error handling and retry logic
- [ ] Caching for improved performance

### 5.3 Could Work (Future)
- [ ] BTS manifest support
- [ ] Stream decryption for protected content
- [ ] Lyrics integration
- [ ] Advanced metadata (credits, etc.)
- [ ] Batch processing optimizations

---

## 6. Implementation Timeline

| Week | Focus | Deliverables | Risk Level |
|------|-------|-------------|------------|
| **1** | Foundation | Project structure, adapters, basic integration | Low |
| **2** | Core Services | TidalSharp port, authentication service | Medium |
| **3** | Integration | Indexer, download client, settings UI | Medium |
| **4** | Testing | End-to-end validation, bug fixes | High |

---

## 7. Architectural Benefits Validated

### 7.1 Shared Library Value Confirmed
- **Reduces complexity** by ~400 lines of Lidarr integration boilerplate
- **Provides tested patterns** for HTTP, caching, quality management
- **Enables focus** on Tidal-specific logic rather than infrastructure
- **Offers consistency** with existing Qobuzarr plugin

### 7.2 TidalSharp Integration Feasible
- **Core streaming logic** is self-contained and portable
- **OAuth implementation** works with real Tidal API
- **Download pipeline** handles chunked streaming successfully
- **Metadata application** uses standard TagLibSharp library

### 7.3 Risk Profile Acceptable
- **Most critical components** (auth, search, download) have working implementations
- **Unknowns are isolated** (BTS support, decryption) and have fallback strategies
- **Dependencies are minimal** and well-established
- **Integration points** are well-defined through adapter pattern

---

## Conclusion

**RECOMMENDATION: PROCEED WITH IMPLEMENTATION**

The analysis confirms that:

1. **TidalSharp provides a solid foundation** for Tidal integration
2. **Shared library approach adds significant value** with minimal overhead
3. **Implementation is feasible** within the 4-week timeline
4. **Risk profile is acceptable** with appropriate mitigation strategies
5. **Architecture scales well** for future enhancements

The combination of TidalSharp's proven streaming logic and the shared library's infrastructure provides the best of both worlds: working Tidal functionality with professional plugin architecture.

Key success factors:
- Port TidalSharp core with minimal changes (preserve working logic)
- Use adapters to bridge TidalSharp and shared library interfaces
- Focus on MPD manifest support initially (BTS as enhancement)
- Test extensively with real Tidal account throughout development
- Leverage shared library for all Lidarr integration complexity

This approach balances **pragmatism** (using proven TidalSharp code) with **architecture quality** (shared library benefits) to deliver a production-ready Tidalarr plugin.
