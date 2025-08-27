# Tidalarr Final Implementation Plan
## Using Lidarr.Plugin.Common Shared Library

---

## Executive Summary

🎉 **GAME CHANGER**: Qobuzarr has implemented the complete shared library we proposed! This dramatically simplifies Tidalarr development from **~1,500 lines** to **~400 lines** of Tidal-specific code.

The shared library provides everything we need: authentication framework, HTTP clients, quality management, caching, testing, and even complete Tidalarr examples.

---

## 1. What's Already Built For Us

### Shared Library Components (Ready to Use):
✅ **BaseStreamingSettings** - Universal plugin settings with validation  
✅ **StreamingApiRequestBuilder** - Fluent HTTP client with retry logic  
✅ **QualityMapper** - Standardized quality tiers (Low → HiRes)  
✅ **BaseStreamingAuthenticationService** - OAuth/Token auth framework  
✅ **StreamingResponseCache** - Intelligent API response caching  
✅ **StreamingPluginModule** - DI registration and lifecycle  
✅ **MockFactories** - Comprehensive testing utilities  

### Complete Tidalarr Examples:
✅ **TidalSettings.cs** - Complete settings implementation  
✅ **TidalIndexer.cs** - Search functionality  
✅ **TidalDownloadClient.cs** - Download orchestration  
✅ **TidalModule.cs** - Plugin registration  

**Result**: We have a working Tidalarr blueprint with only Tidal-specific logic needed!

---

## 2. Revised Project Structure

```
Tidalarr/
├── src/
│   ├── Tidalarr.csproj                 # References Lidarr.Plugin.Common
│   │
│   ├── Settings/
│   │   └── TidalSettings.cs            # 50 lines (extend BaseStreamingSettings)
│   │
│   ├── Services/                       # Only Tidal-specific services
│   │   ├── TidalAuthenticationService.cs  # 150 lines (OAuth + Token)
│   │   ├── TidalApiService.cs             # 100 lines (API calls)
│   │   └── TidalStreamProcessor.cs        # 200 lines (port from TidalSharp)
│   │
│   ├── Integration/                    # Lidarr plugin interfaces
│   │   ├── TidalIndexer.cs            # 150 lines (uses shared library)
│   │   └── TidalDownloadClient.cs     # 200 lines (uses shared library)
│   │
│   ├── Models/                         # Tidal-specific models only
│   │   └── TidalModels.cs             # 100 lines (API DTOs)
│   │
│   └── TidalModule.cs                  # 50 lines (DI registration)
│
├── TidalCLI/                           # Test bed application
│   └── [Same structure as planned]     # References main plugin
│
└── plugin.json                        # Plugin manifest
```

**Total Implementation**: ~900 lines instead of 3,500+ lines (74% reduction!)

---

## 3. Implementation Breakdown

### 3.1 Settings (50 lines total)

```csharp
// TidalSettings.cs - Extend shared library base
public class TidalSettings : BaseStreamingSettings, IIndexerSettings
{
    // Tidal-specific authentication
    [FieldDefinition(10, Label = "Tidal API Token", Type = FieldType.Password)]
    public string TidalApiToken { get; set; }
    
    [FieldDefinition(11, Label = "Tidal Market", Type = FieldType.Select,
                     SelectOptions = new[] { "US", "UK", "DE", "FR" })]
    public string TidalMarket { get; set; } = "US";
    
    [FieldDefinition(12, Label = "Subscription Tier", Type = FieldType.Select,
                     SelectOptions = new[] { "Free", "Premium", "HiFi", "HiFi Plus" })]
    public int SubscriptionTier { get; set; } = (int)TidalSubscriptionTier.HiFi;
    
    // Tidal-specific features
    [FieldDefinition(13, Label = "Include MQA", Type = FieldType.Checkbox)]
    public bool IncludeMqa { get; set; } = true;
    
    // Override validation for Tidal-specific rules
    public override bool IsValid(out string errorMessage)
    {
        if (!base.IsValid(out errorMessage))
            return false;
            
        if (string.IsNullOrEmpty(TidalApiToken))
        {
            errorMessage = "Tidal API token is required";
            return false;
        }
        
        return true;
    }
}
```

### 3.2 Authentication Service (150 lines)

```csharp
// TidalAuthenticationService.cs - Extend shared auth framework
public class TidalAuthenticationService : BaseStreamingAuthenticationService<TidalSession, TidalCredentials>
{
    // Use the shared library's fluent HTTP builder
    protected override async Task<TidalSession> CreateSessionAsync(TidalCredentials credentials)
    {
        var request = new StreamingApiRequestBuilder("https://auth.tidal.com/v1/oauth2/token")
            .Method(HttpMethod.Post)
            .Header("Content-Type", "application/x-www-form-urlencoded")
            .WithStreamingDefaults("Tidalarr/1.0")
            .Body(BuildOAuthBody(credentials))
            .Build();
            
        var response = await HttpClient.ExecuteWithRetryAsync<TidalTokenResponse>(request);
        return MapToSession(response);
    }
    
    protected override async Task<TidalSession> RefreshSessionAsync(TidalSession session)
    {
        // Use shared retry logic and error classification
        // Only implement Tidal-specific refresh logic
    }
    
    // Tidal-specific OAuth PKCE implementation
    private string BuildOAuthBody(TidalCredentials credentials) { /* ~30 lines */ }
}
```

### 3.3 API Service (100 lines)

```csharp
// TidalApiService.cs - Wrapper around Tidal API
public class TidalApiService
{
    private readonly StreamingApiRequestBuilder _requestBuilder;
    private readonly TidalAuthenticationService _authService;
    
    public async Task<TidalSearchResponse> SearchAsync(string query, int limit = 100)
    {
        await _authService.EnsureValidSessionAsync();
        
        var request = _requestBuilder
            .BaseUrl("https://api.tidal.com/v1/")
            .Endpoint("search/albums")
            .Query("query", query)
            .Query("limit", limit.ToString())
            .BearerToken(_authService.CurrentSession.AccessToken)
            .WithStreamingDefaults("Tidalarr/1.0")
            .Build();
            
        return await HttpClient.ExecuteWithRetryAsync<TidalSearchResponse>(request);
    }
    
    // Similar methods for GetAlbum, GetTrack, GetStreamUrl
}
```

### 3.4 Indexer (150 lines)

```csharp
// TidalIndexer.cs - Use shared library patterns
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
            
        // Search with Tidal API
        var results = await _apiService.SearchAsync(request.SearchCriteria.SearchTerm);
        
        // Map using shared library models
        var releases = results.Albums.Select(album => new ReleaseInfo
        {
            Title = album.Title,
            Artist = string.Join(", ", album.Artists.Select(a => a.Name)),
            DownloadUrl = BuildDownloadUrl(album.Id),
            // Use shared quality mapper
            Quality = _qualityMapper.MapToLidarrQuality(album.AudioQuality)
        }).ToList();
        
        // Cache with shared library
        _cache.Set(cacheKey, releases, TimeSpan.FromMinutes(Settings.CacheDuration));
        return releases;
    }
}
```

### 3.5 Download Client (200 lines)

```csharp
// TidalDownloadClient.cs - Integrate with TidalSharp
public class TidalDownloadClient : DownloadClientBase<TidalSettings>
{
    private readonly TidalStreamProcessor _streamProcessor; // From TidalSharp port
    private readonly TidalApiService _apiService;
    
    public override async Task<string> Download(RemoteAlbum remoteAlbum, IIndexer indexer)
    {
        // Use shared download orchestration patterns
        var albumId = ExtractAlbumId(remoteAlbum.Release.DownloadUrl);
        var album = await _apiService.GetAlbumAsync(albumId);
        
        foreach (var track in album.Tracks)
        {
            // Get stream URL using shared HTTP builder
            var streamInfo = await GetStreamInfoAsync(track.Id, Settings.PreferredQuality);
            
            // Use ported TidalSharp logic for actual download
            var trackData = await _streamProcessor.DownloadTrackAsync(streamInfo);
            
            // Apply metadata using shared library utilities
            await ApplyMetadataAsync(trackData, track, album);
        }
        
        return GenerateJobId();
    }
    
    // Port only the essential TidalSharp download logic here
    // ~100 lines of stream processing, chunk downloading, decryption
}
```

---

## 4. TidalSharp Integration Strategy

### What to Port Directly:
1. **Decryption Logic** - Keep the exact AES decryption implementation
2. **Manifest Parsing** - MPD/BTS parsing (focus on MPD)
3. **Chunk Downloading** - Sequential download and assembly
4. **Stream URL Extraction** - From playback info endpoint

### What to Replace with Shared Library:
1. **HTTP Client** → Use `StreamingApiRequestBuilder`
2. **Authentication** → Use `BaseStreamingAuthenticationService`
3. **Caching** → Use `StreamingResponseCache`
4. **Error Handling** → Use shared retry and classification
5. **Quality Management** → Use `QualityMapper`

### Integration Pattern:
```csharp
// TidalStreamProcessor.cs - Thin wrapper around TidalSharp core
public class TidalStreamProcessor
{
    private readonly TidalDecryption _decryption;    // Direct from TidalSharp
    private readonly TidalManifestParser _parser;    // Direct from TidalSharp
    private readonly StreamingApiRequestBuilder _httpBuilder; // From shared lib
    
    public async Task<byte[]> DownloadTrackAsync(TidalStreamInfo streamInfo)
    {
        // Use shared HTTP builder for stream requests
        var chunks = await DownloadChunksAsync(streamInfo.ChunkUrls);
        var assembled = AssembleChunks(chunks); // From TidalSharp
        
        if (streamInfo.IsEncrypted)
        {
            return _decryption.DecryptStream(assembled, streamInfo.SecurityToken);
        }
        
        return assembled;
    }
}
```

---

## 5. Simplified Timeline

### Week 1: Foundation
**Day 1-2**: Project setup with shared library reference  
**Day 3-4**: Implement TidalSettings and basic TidalModule  
**Day 5**: TidalAuthenticationService with OAuth support  

### Week 2: Core Services  
**Day 1-2**: TidalApiService using shared HTTP builder  
**Day 3-4**: Port essential TidalSharp logic to TidalStreamProcessor  
**Day 5**: Quality mapping and model conversion  

### Week 3: Integration
**Day 1-2**: TidalIndexer implementation  
**Day 3-4**: TidalDownloadClient with stream processing  
**Day 5**: End-to-end testing and debugging  

### Week 4: Polish & CLI
**Day 1-2**: TidalCLI test bed application  
**Day 3-4**: Comprehensive testing with real Tidal account  
**Day 5**: Documentation and packaging  

**Total: 4 weeks** (same timeline, but 74% less code!)

---

## 6. Key Benefits of Shared Library Approach

### Development Benefits:
- **74% less code to write** (400 vs 1,500+ lines)
- **Proven patterns** from battle-tested Qobuzarr
- **Complete examples** to follow
- **Built-in testing** support

### Quality Benefits:
- **Production-ready components** (security, performance, reliability)
- **Standardized behavior** across all streaming plugins
- **Comprehensive error handling** built-in
- **Thread-safe operations** with proper patterns

### Maintenance Benefits:
- **Shared bug fixes** benefit all plugins
- **Consistent updates** across ecosystem
- **Easier debugging** with standardized patterns
- **Community support** for common issues

---

## 7. What We DON'T Need to Build

❌ ~~Custom HTTP client~~ → Use `StreamingApiRequestBuilder`  
❌ ~~Authentication framework~~ → Use `BaseStreamingAuthenticationService`  
❌ ~~Response caching~~ → Use `StreamingResponseCache`  
❌ ~~Quality management~~ → Use `QualityMapper`  
❌ ~~Settings validation~~ → Use `BaseStreamingSettings`  
❌ ~~Plugin registration~~ → Use `StreamingPluginModule`  
❌ ~~Test utilities~~ → Use `MockFactories`  
❌ ~~Error classification~~ → Built into shared services  
❌ ~~Retry logic~~ → Built into shared HTTP client  
❌ ~~Progress tracking~~ → Built into shared download patterns  

---

## 8. Critical Success Path

### Must Work:
1. ✅ **Shared library integration** - Reference and basic setup
2. 🔄 **Tidal OAuth authentication** - Using shared auth framework
3. 🔄 **Search functionality** - Using shared HTTP builder
4. 🔄 **Quality detection** - Using shared quality mapper
5. 🔄 **Stream URL acquisition** - Tidal API integration
6. 🔄 **File download** - Port TidalSharp core logic
7. 🔄 **Metadata application** - Using shared utilities

### Can be Enhanced Later:
- BTS manifest support (MPD is sufficient)
- Advanced error recovery
- Performance optimizations
- Additional quality tiers
- Lyrics integration

---

## 9. Testing Strategy

Using the shared library's `MockFactories`:

```csharp
[Test]
public async Task TidalIndexer_Search_ReturnsResults()
{
    // Use shared library test utilities
    var mockApi = MockFactories.CreateMockApiService<TidalApiService>();
    var mockCache = MockFactories.CreateMockCache();
    
    var indexer = new TidalIndexer(mockApi, mockCache, /* other deps */);
    
    // Test with realistic data
    var results = await indexer.FetchReleases(MockFactories.CreateSearchRequest("test"));
    
    Assert.That(results.Count, Is.GreaterThan(0));
}
```

---

## Conclusion

The shared library represents a **massive win** for Tidalarr development:

- **74% less code** to write and maintain
- **Battle-tested architecture** from working Qobuzarr  
- **Complete working examples** for every component
- **Production-ready quality** from day one
- **Easy integration** with existing TidalSharp logic
- **Future-proof foundation** for ecosystem growth

**Recommendation**: Proceed with shared library approach immediately. The examples provide a complete roadmap, and we can have a working Tidalarr plugin in 4 weeks with significantly higher quality than originally planned.

This is the difference between building a prototype and building a production-ready, enterprise-grade plugin that can scale with the ecosystem.