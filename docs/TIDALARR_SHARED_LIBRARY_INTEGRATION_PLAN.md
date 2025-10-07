# Tidalarr Shared Library Integration Plan

## Executive Summary

This plan outlines the comprehensive integration of Tidalarr with the Lidarr.Plugin.Common shared library to achieve 60-70% code reduction while improving reliability, performance, and maintainability.

## Current State Analysis

### Strengths
- ✅ OAuth 2.0 + PKCE authentication properly implemented
- ✅ Clean architecture with separated concerns
- ✅ Already uses some shared library components (settings, utilities)
- ✅ Quality detection and mapping system in place
- ✅ Chunk-based download for streaming content

### Critical Issues
- ❌ **Incorrect base class inheritance**: `TidalDownloadClient` extends `BaseDownloadOrchestrator` instead of `BaseStreamingDownloadClient`
- ❌ **No proper indexer base class**: `TidalIndexer` doesn't extend `BaseStreamingIndexer`
- ❌ **Incomplete album track loading**: Albums don't enumerate tracks properly
- ❌ **Missing shared services integration**: Not using shared caching, rate limiting, performance monitoring

## Integration Phases

### Phase 1: Critical Fixes (Week 1)
Fix fundamental inheritance and integration issues that block proper Lidarr integration.

#### 1.1 Fix Download Client Inheritance
**Current**: `TidalDownloadClient : BaseDownloadOrchestrator<TidalTrackInfo, TidalAlbumInfo, TidalSettings>`
**Target**: `TidalDownloadClient : BaseStreamingDownloadClient<TidalSettings>`

**Tasks**:
- [ ] Refactor TidalDownloadClient to extend BaseStreamingDownloadClient
- [ ] Map TidalTrackInfo/TidalAlbumInfo to StreamingTrack/StreamingAlbum models
- [ ] Implement required abstract methods from base class
- [ ] Remove redundant download orchestration code

#### 1.2 Fix Indexer Inheritance
**Current**: `TidalIndexer` (standalone class)
**Target**: `TidalIndexer : BaseStreamingIndexer<TidalSettings>`

**Tasks**:
- [ ] Refactor TidalIndexer to extend BaseStreamingIndexer
- [ ] Implement abstract search methods
- [ ] Leverage base class authentication and session management
- [ ] Remove redundant search logic

#### 1.3 Fix Album Track Loading
**Tasks**:
- [ ] Implement proper track enumeration in GetAlbumAsync
- [ ] Add track metadata population
- [ ] Ensure track ordering is preserved

### Phase 2: Model Alignment (Week 1-2)
Align Tidal models with shared library models for seamless integration.

#### 2.1 Create Model Mappers
**Tasks**:
- [ ] Create TidalToStreamingMapper class
- [ ] Map TidalTrackInfo → StreamingTrack
- [ ] Map TidalAlbumInfo → StreamingAlbum
- [ ] Map TidalArtistInfo → StreamingArtist
- [ ] Map TidalQuality → StreamingQuality

#### 2.2 Refactor Internal Models
**Tasks**:
- [ ] Keep Tidal-specific models for API responses
- [ ] Use shared models for Lidarr integration
- [ ] Create extension methods for model conversion

### Phase 3: Service Integration (Week 2)
Integrate shared library services for improved functionality.

#### 3.1 Authentication Service Integration
**Current**: Custom TidalOAuthService
**Target**: Extend OAuthStreamingAuthenticationService

**Tasks**:
- [ ] Refactor TidalOAuthService to properly extend base class
- [ ] Implement IStreamingTokenProvider interface
- [ ] Use shared session management
- [ ] Leverage automatic token refresh

#### 3.2 HTTP Client Integration
**Tasks**:
- [ ] Replace custom HTTP logic with StreamingApiRequestBuilder
- [ ] Implement request signing if needed
- [ ] Add proper retry policies using shared utilities
- [ ] Configure service-specific headers

#### 3.3 Caching Integration
**Tasks**:
- [ ] Implement TidalResponseCache : StreamingResponseCache
- [ ] Configure cache durations per endpoint
- [ ] Add cache key generation for Tidal specifics
- [ ] Enable cache statistics tracking

#### 3.4 Rate Limiting Integration
**Tasks**:
- [ ] Configure AdaptiveRateLimiter for Tidal API limits
- [ ] Set per-endpoint rate limits
- [ ] Implement 429 response handling
- [ ] Add rate limit metrics collection

### Phase 4: Quality & Performance (Week 3)
Enhance quality management and performance using shared infrastructure.

#### 4.1 Quality Mapper Integration
**Tasks**:
- [ ] Replace custom quality detection with QualityMapper
- [ ] Map Tidal quality tiers to shared quality model
- [ ] Implement quality preference matching
- [ ] Add quality availability detection

#### 4.2 Performance Monitoring
**Tasks**:
- [ ] Integrate PerformanceMonitor for all operations
- [ ] Add download speed tracking
- [ ] Implement success/failure metrics
- [ ] Configure alerting thresholds

#### 4.3 Batch Operations
**Tasks**:
- [ ] Use shared batch download infrastructure
- [ ] Implement concurrent album downloads
- [ ] Add progress reporting using shared models
- [ ] Configure optimal concurrency limits

### Phase 5: Advanced Features (Week 3-4)
Leverage advanced shared library features for enhanced functionality.

#### 5.1 Intelligence Services
**Tasks**:
- [ ] Integrate CompilationAlbumDetector
- [ ] Implement IQueryOptimizer for search
- [ ] Add live album detection
- [ ] Configure remixes/versions handling

#### 5.2 Security Enhancements
**Tasks**:
- [ ] Use SecureCredentialManager for token storage
- [ ] Implement InputSanitizer for all user inputs
- [ ] Add secure logging with sensitive data masking
- [ ] Configure security validation

#### 5.3 CLI Integration (Optional)
**Tasks**:
- [ ] Create TidalCLI : BaseStreamingCLI<TidalSettings>
- [ ] Implement service-specific commands
- [ ] Add interactive authentication flow
- [ ] Configure rich console output

### Phase 6: Testing & Validation (Week 4)
Ensure robust integration through comprehensive testing.

#### 6.1 Unit Testing
**Tasks**:
- [ ] Create tests for model mappers
- [ ] Test authentication flows
- [ ] Validate quality detection
- [ ] Test error handling scenarios

#### 6.2 Integration Testing
**Tasks**:
- [ ] Test Lidarr indexer integration
- [ ] Validate download client functionality
- [ ] Test album/track downloads
- [ ] Verify metadata accuracy

#### 6.3 Performance Testing
**Tasks**:
- [ ] Benchmark download speeds
- [ ] Test concurrent operations
- [ ] Validate rate limiting
- [ ] Measure memory usage

## Implementation Details

### Key Files to Modify

#### 1. TidalDownloadClient.cs
```csharp
public class TidalDownloadClient : BaseStreamingDownloadClient<TidalSettings>
{
    protected override string ServiceName => "Tidal";
    protected override string ProtocolName => "tidal";
    
    protected override async Task<StreamingTrack> GetTrackAsync(string trackId)
    {
        var tidalTrack = await _apiClient.GetTrackAsync(trackId);
        return _mapper.ToStreamingTrack(tidalTrack);
    }
    
    protected override async Task<byte[]> DownloadTrackDataAsync(
        StreamingTrack track, 
        StreamingQuality quality,
        CancellationToken cancellationToken)
    {
        // Use existing chunk downloader
        var streamInfo = await _streamService.GetStreamInfoAsync(track.Id, quality);
        return await _chunkDownloader.DownloadAsync(streamInfo, cancellationToken);
    }
}
```

#### 2. TidalIndexer.cs
```csharp
public class TidalIndexer : BaseStreamingIndexer<TidalSettings>
{
    protected override string ServiceName => "Tidal";
    protected override string ProtocolName => "tidal";
    
    protected override async Task<List<StreamingAlbum>> SearchAlbumsAsync(string searchTerm)
    {
        var results = await _apiClient.SearchAlbumsAsync(searchTerm);
        return results.Select(_mapper.ToStreamingAlbum).ToList();
    }
}
```

#### 3. TidalOAuthService.cs
```csharp
public class TidalOAuthService : OAuthStreamingAuthenticationService<TidalTokens, TidalCredentials>
{
    protected override string AuthorizationUrl => "https://login.tidal.com/authorize";
    protected override string TokenUrl => "https://auth.tidal.com/v1/oauth2/token";
    
    protected override async Task<TidalTokens> ExchangeCodeForTokenAsync(string code)
    {
        // Existing PKCE token exchange logic
    }
}
```

### New Files to Create

#### 1. TidalModelMapper.cs
```csharp
public class TidalModelMapper
{
    public StreamingTrack ToStreamingTrack(TidalTrackInfo track) { }
    public StreamingAlbum ToStreamingAlbum(TidalAlbumInfo album) { }
    public StreamingQuality ToStreamingQuality(TidalQuality quality) { }
}
```

#### 2. TidalResponseCache.cs
```csharp
public class TidalResponseCache : StreamingResponseCache
{
    protected override TimeSpan GetCacheDuration(string endpoint)
    {
        return endpoint switch
        {
            _ when endpoint.Contains("/search") => TimeSpan.FromMinutes(5),
            _ when endpoint.Contains("/albums") => TimeSpan.FromHours(1),
            _ when endpoint.Contains("/tracks") => TimeSpan.FromHours(1),
            _ => TimeSpan.FromMinutes(15)
        };
    }
}
```

#### 3. TidalCLI.cs (Optional)
```csharp
public class TidalCLI : BaseStreamingCLI<TidalSettings>
{
    protected override string ServiceName => "Tidal";
    protected override string ServiceDescription => "Tidal HiFi streaming service";
    
    protected override IStreamingIndexer CreateIndexer(TidalSettings settings)
        => new TidalIndexer(settings);
    
    protected override IStreamingDownloadClient CreateDownloadClient(TidalSettings settings)
        => new TidalDownloadClient(settings);
}
```

## Benefits & Expected Outcomes

### Code Reduction
- **60-70% less code** to maintain
- Remove ~2000 lines of redundant code
- Focus only on Tidal-specific logic

### Improved Reliability
- Battle-tested error handling
- Automatic retries and fallbacks
- Consistent logging and monitoring

### Enhanced Performance
- Adaptive rate limiting prevents API bans
- Response caching reduces API calls
- Concurrent downloads with progress tracking

### Better Maintainability
- Standardized patterns across plugins
- Centralized bug fixes in shared library
- Comprehensive testing infrastructure

### New Capabilities
- Compilation album detection
- Query optimization
- Secure credential storage
- Rich CLI interface (optional)

## Success Metrics

1. **Code Metrics**
   - [ ] 60%+ reduction in custom code
   - [ ] 100% test coverage for critical paths
   - [ ] Zero duplicate implementations

2. **Performance Metrics**
   - [ ] <100ms search response time (cached)
   - [ ] >5MB/s download speeds
   - [ ] <1% API error rate

3. **Integration Metrics**
   - [ ] Full Lidarr compatibility
   - [ ] Seamless authentication flow
   - [ ] Accurate metadata mapping

## Risk Mitigation

### Risk 1: Breaking Changes
**Mitigation**: Implement changes incrementally with thorough testing at each phase.

### Risk 2: API Incompatibilities
**Mitigation**: Keep Tidal-specific logic isolated in service implementations.

### Risk 3: Performance Degradation
**Mitigation**: Benchmark before and after each phase, rollback if needed.

## Timeline

- **Week 1**: Critical Fixes (Phases 1-2)
- **Week 2**: Service Integration (Phase 3)
- **Week 3**: Quality & Advanced Features (Phases 4-5)
- **Week 4**: Testing & Validation (Phase 6)

## Next Steps

1. **Immediate Actions**:
   - Fix TidalDownloadClient inheritance issue
   - Fix TidalIndexer to extend base class
   - Implement album track loading

2. **Quick Wins**:
   - Add response caching
   - Integrate rate limiting
   - Use shared quality mapper

3. **Long-term Goals**:
   - Full CLI implementation
   - ML-based search optimization
   - Advanced duplicate detection

## Conclusion

This integration plan provides a clear roadmap to transform Tidalarr into a robust, maintainable plugin that leverages 60-70% of its functionality from the battle-tested Lidarr.Plugin.Common library. The phased approach ensures minimal disruption while maximizing the benefits of the shared infrastructure.
