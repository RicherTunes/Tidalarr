> ⚠️ Historical (flagged 2026-05-31): describes a past state; some details below no longer match the current code.

# Chief Architect Recommendations: Lidarr.Plugin.Common Enhancement Strategy

## Executive Summary

After comprehensive analysis of Tidalarr, Qobuzarr, and TrevTV's Tidal plugin implementations, I've identified critical opportunities to enhance the `Lidarr.Plugin.Common` shared library. This document provides specific architectural recommendations to maximize code reuse and improve the streaming plugin ecosystem.

## Current State Assessment

### Strengths
- ✅ Basic shared utilities already extracted (FileNameSanitizer, HttpClientExtensions, RetryUtilities)
- ✅ Foundation authentication service pattern established
- ✅ Quality mapping abstractions in place
- ✅ Security primitives (InputSanitizer, SecureCredentialManager) available

### Gaps Identified
- ❌ **OAuth/PKCE authentication** not generalized (duplicated in Tidalarr & TrevTV's)
- ❌ **Advanced rate limiting** not promoted from Qobuzarr
- ❌ **Batch memory management** unique to Qobuzarr, needed by others
- ❌ **Download orchestration framework** completely missing
- ❌ **Compilation album detection** only in temp version, not in ext version

## Priority 1: Critical Additions (Immediate Impact)

### 1.1 OAuth Authentication Framework

**Problem:** Both Tidalarr and TrevTV's implement OAuth PKCE flows independently

**Solution:** Extract and generalize OAuth patterns
```csharp
namespace Lidarr.Plugin.Common.Services.Authentication
{
    public abstract class OAuthStreamingAuthenticationService<TSession, TCredentials>
        : BaseStreamingAuthenticationService<TSession, TCredentials>
    {
        protected readonly IPKCEGenerator _pkceGenerator;
        
        protected abstract string GetAuthorizationUrl(string codeChallenge, string state);
        protected abstract Task<TSession> ExchangeCodeForTokenAsync(string code, string verifier);
        protected abstract Task<TSession> RefreshTokenAsync(string refreshToken);
        
        public virtual async Task<string> InitiateOAuthFlowAsync()
        {
            var (codeVerifier, codeChallenge) = _pkceGenerator.Generate();
            // Store verifier, return auth URL
        }
    }
    
    public class PKCEGenerator : IPKCEGenerator
    {
        // Move from Tidalarr's implementation
    }
}
```

### 1.2 Universal Adaptive Rate Limiter

**Problem:** Each plugin implements rate limiting differently, Qobuzarr has the most sophisticated

**Solution:** Promote Qobuzarr's AdaptiveRateLimiter to shared library
```csharp
namespace Lidarr.Plugin.Common.Services.Performance
{
    public class UniversalAdaptiveRateLimiter : IAdaptiveRateLimiter
    {
        // From Qobuzarr's implementation with enhancements:
        // - Per-service, per-endpoint rate tracking
        // - Success-based rate increases
        // - Failure-based backoff
        // - Statistical reporting
        // - Multi-service support
        
        public async Task<bool> WaitIfNeededAsync(
            string service, 
            string endpoint, 
            CancellationToken ct = default);
            
        public void RecordSuccess(string service, string endpoint);
        public void RecordFailure(string service, string endpoint, int? retryAfterSeconds = null);
    }
}
```

### 1.3 Batch Memory Manager

**Problem:** Large album downloads cause OOM issues, only Qobuzarr handles this properly

**Solution:** Extract Qobuzarr's BatchMemoryManager
```csharp
namespace Lidarr.Plugin.Common.Services.Performance
{
    public class BatchMemoryManager : IBatchMemoryManager
    {
        // From Qobuzarr with universal interface:
        public int GetOptimalBatchSize<T>();
        public bool ShouldPauseForMemory();
        public void RecordBatchCompletion(int itemsProcessed, long bytesProcessed);
    }
}
```

### 1.4 Compilation Album Detector

**Problem:** Various Artists albums fail matching, solution exists in temp but not deployed

**Solution:** Deploy CompilationAlbumDetector from temp to production
```csharp
namespace Lidarr.Plugin.Common.Services.Intelligence
{
    public class CompilationAlbumDetector
    {
        // Already implemented in temp/Lidarr.Plugin.Common
        // Move to production ext/Lidarr.Plugin.Common
        public bool IsVariousArtists(string albumName, string artistName);
        public CompilationType GetCompilationType(string albumName, string artistName);
        public MatchingStrategy GetMatchingStrategy(CompilationType type);
    }
}
```

## Priority 2: Framework Enhancements (Short Term)

### 2.1 Base Download Orchestrator

**Pattern to Extract:**
```csharp
namespace Lidarr.Plugin.Common.Services.Download
{
    public abstract class BaseDownloadOrchestrator<TTrack, TAlbum, TSettings>
    {
        protected readonly IAdaptiveRateLimiter _rateLimiter;
        protected readonly IBatchMemoryManager _memoryManager;
        protected readonly ILogger _logger;
        
        public abstract Task<DownloadResult> DownloadAlbumAsync(
            TAlbum album, 
            TSettings settings,
            IProgress<DownloadProgress> progress = null);
            
        protected virtual async Task<byte[]> DownloadTrackAsync(
            TTrack track,
            TSettings settings)
        {
            // Common download logic with rate limiting
            await _rateLimiter.WaitIfNeededAsync(ServiceName, "download");
            // ... download implementation
        }
        
        protected virtual string GenerateFilePath(TTrack track, TAlbum album, string outputDir)
        {
            // Use FileNameSanitizer
        }
    }
}
```

### 2.2 Enhanced Streaming API Client

**Consolidate patterns from all three:**
```csharp
namespace Lidarr.Plugin.Common.Services.Http
{
    public class EnhancedStreamingApiClient : IStreamingApiClient
    {
        private readonly IAdaptiveRateLimiter _rateLimiter;
        private readonly IStreamingAuthenticationService _authService;
        private readonly IStreamingResponseCache _cache;
        
        public async Task<T> GetAsync<T>(
            string service,
            string endpoint, 
            Dictionary<string, string> parameters = null,
            CachePolicy cachePolicy = null)
        {
            // Rate limiting
            await _rateLimiter.WaitIfNeededAsync(service, endpoint);
            
            // Cache check
            if (cachePolicy != null && _cache.TryGet(key, out T cached))
                return cached;
                
            // Build request with auth
            var request = new StreamingApiRequestBuilder(baseUrl)
                .Endpoint(endpoint)
                .QueryParameters(parameters)
                .BearerToken(await _authService.GetTokenAsync())
                .Build();
                
            // Execute with retry
            var response = await HttpClientExtensions.ExecuteWithRetryAsync(
                _httpClient, 
                request,
                _rateLimiter.GetRetryPolicy(service));
                
            // Cache and return
            if (cachePolicy != null)
                _cache.Set(key, result, cachePolicy);
                
            return result;
        }
    }
}
```

## Priority 3: Advanced Features (Medium Term)

### 3.1 Query Optimization Interface

**From Qobuzarr's ML patterns:**
```csharp
namespace Lidarr.Plugin.Common.Services.Intelligence
{
    public interface IQueryOptimizer
    {
        string OptimizeQuery(string originalQuery, QueryContext context);
        void RecordResult(string query, bool successful);
        QueryComplexity PredictComplexity(string query);
    }
    
    public class SimplePatternOptimizer : IQueryOptimizer
    {
        // Basic implementation without ML
    }
}
```

### 3.2 Secure Operations Wrapper

**Extract defensive patterns:**
```csharp
namespace Lidarr.Plugin.Common.Services
{
    public static class SafeStreamingOperations
    {
        public static async Task<(bool Success, T Result, Exception Error)> 
            ExecuteAsync<T>(Func<Task<T>> operation, string operationName = null);
            
        public static string SanitizeApiInput(string input, InputType type);
        
        public static Dictionary<string, string> MaskSensitiveParameters(
            Dictionary<string, string> parameters,
            params string[] sensitiveKeys);
    }
}
```

## Implementation Roadmap

### Phase 1 (Week 1-2)
1. ✅ Sync CompilationAlbumDetector from temp to ext
2. ✅ Extract PKCEGenerator from Tidalarr
3. ✅ Promote AdaptiveRateLimiter from Qobuzarr
4. ✅ Promote BatchMemoryManager from Qobuzarr

### Phase 2 (Week 3-4)
1. ⏳ Implement OAuthStreamingAuthenticationService base class
2. ⏳ Create UniversalAdaptiveRateLimiter with multi-service support
3. ⏳ Build EnhancedStreamingApiClient with integrated features

### Phase 3 (Week 5-6)
1. ⏳ Extract BaseDownloadOrchestrator framework
2. ⏳ Implement IQueryOptimizer interface and basic implementation
3. ⏳ Add SafeStreamingOperations utility class

## Migration Guide for Existing Plugins

### Tidalarr Migration
```csharp
// Before
public class TidalOAuthService
{
    private readonly PKCEGenerator _pkceGenerator;
    // Custom OAuth implementation
}

// After
public class TidalOAuthService : OAuthStreamingAuthenticationService<TidalSession, TidalCredentials>
{
    protected override string GetAuthorizationUrl(string codeChallenge, string state)
    {
        return $"https://login.tidal.com/authorize?...";
    }
}
```

### Qobuzarr Contribution
```csharp
// Move these to shared library:
// - AdaptiveRateLimiter → Lidarr.Plugin.Common.Services.Performance
// - BatchMemoryManager → Lidarr.Plugin.Common.Services.Performance
// - SecureCredentialManager → Lidarr.Plugin.Common.Security
// - DefensiveServiceWrapper → Lidarr.Plugin.Common.Services

// Then reference from shared library instead of local implementation
```

## Success Metrics

### Code Reduction Targets
- **OAuth Implementation:** 200+ lines → 50 lines (75% reduction)
- **Rate Limiting:** 150+ lines → 20 lines (87% reduction)
- **Download Orchestration:** 400+ lines → 100 lines (75% reduction)
- **Overall Plugin Size:** 3500 lines → 1000 lines (71% reduction)

### Quality Improvements
- ✅ Consistent error handling across all plugins
- ✅ Unified rate limiting prevents API bans
- ✅ Memory management prevents OOM crashes
- ✅ Security improvements through shared validation

## Risk Mitigation

### Backward Compatibility
- All changes are additive, no breaking changes to existing APIs
- Provide compatibility shims for deprecated patterns
- Version shared library with semantic versioning

### Testing Strategy
- Unit tests for all shared components
- Integration tests with mock streaming services
- Migration tests for existing plugins

## Conclusion

The proposed enhancements to `Lidarr.Plugin.Common` will:
1. **Reduce code duplication** by 70%+ across streaming plugins
2. **Improve reliability** through battle-tested shared components
3. **Accelerate development** of new streaming service integrations
4. **Enhance security** with centralized validation and sanitization

Qobuzarr's advanced implementations should be the primary source for shared components, as they represent the most production-hardened patterns. Tidalarr can immediately benefit from these shared components while contributing its OAuth/PKCE patterns back to the library.

The phased approach ensures we deliver immediate value while building toward a comprehensive shared framework for all Lidarr streaming plugins.

---
*Chief Architect Review Complete*
*Recommendation: Proceed with Phase 1 immediately*
