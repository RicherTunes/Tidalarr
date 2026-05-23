> **Note:** This document is historical and may not reflect current architecture. It was one of several iteration plans created during development. See CLAUDE.md for current guidance.

# Tidalarr Final Implementation Plan v3
## Incorporating Chief Architect Feedback + Dedicated Shared Library

---

## Executive Summary

This plan incorporates critical feedback from the Chief Architect and leverages the new dedicated `Lidarr.Plugin.Common` NuGet package. Key changes include abstraction layers for tech debt prevention, resilience patterns, and a focus on secure, maintainable architecture.

**Timeline**: 3 weeks (reduced from 4 due to shared library maturity)  
**Code Reduction**: 74% confirmed by dedicated repository examples  
**Tech Debt**: Proactively prevented through abstraction and boundary patterns  

---

## 1. Architecture Overview with Chief Architect Improvements

```
┌─────────────────────────────────────────────┐
│           Lidarr Integration Layer          │
│     (TidalIndexer, TidalDownloadClient)     │
│                                             │
│  Uses: Lidarr.Plugin.Common (NuGet)        │
└─────────────────────────────────────────────┘
                        │
┌─────────────────────────────────────────────┐
│         Application/Orchestration           │
│    (TidalService, TidalOrchestrator)        │
│                                             │
│  Contains: Use cases, validation, metrics   │
└─────────────────────────────────────────────┘
                        │
┌─────────────────────────────────────────────┐
│            Core Domain Layer                │
│     (ITidalCore, ITidalAuth, Models)        │
│                                             │
│  Contains: Interfaces, domain models        │
└─────────────────────────────────────────────┘
                        │
┌─────────────────────────────────────────────┐
│        Infrastructure/Adapters              │
│   (TidalCoreAdapter, TidalAuthAdapter)      │
│                                             │
│  Contains: TidalSharp integration           │
└─────────────────────────────────────────────┘
```

### Key Architectural Principles:
1. **Domain boundaries enforced** through folder structure and interfaces
2. **TidalSharp isolated** in infrastructure layer with adapters
3. **Shared library** handles all Lidarr integration complexity
4. **Resilience patterns** built into every external call

---

## 2. Project Structure with Boundaries

```
Tidalarr/
├── src/
│   ├── Tidalarr.csproj                    # NuGet ref: Lidarr.Plugin.Common
│   │
│   ├── Core/                              # Domain layer (no external deps)
│   │   ├── Interfaces/
│   │   │   ├── ITidalCore.cs             # Abstract TidalSharp operations
│   │   │   ├── ITidalAuth.cs             # Abstract authentication
│   │   │   ├── ITidalStreamProcessor.cs  # Abstract streaming
│   │   │   └── ISecureTokenStore.cs      # Abstract token storage (future)
│   │   ├── Models/
│   │   │   ├── TidalTrackInfo.cs         # Domain models
│   │   │   ├── TidalQuality.cs           
│   │   │   └── TidalStreamData.cs        
│   │   └── Exceptions/
│   │       ├── TidalApiException.cs      # Domain exceptions
│   │       └── TidalAuthException.cs     
│   │
│   ├── Application/                       # Use cases and orchestration
│   │   ├── Services/
│   │   │   ├── TidalService.cs           # Main service orchestrator
│   │   │   ├── TidalSearchService.cs     # Search use cases
│   │   │   └── TidalDownloadService.cs   # Download use cases
│   │   ├── Validators/
│   │   │   └── TidalRequestValidator.cs  # Input validation
│   │   └── Metrics/
│   │       └── TidalMetrics.cs           # Telemetry and monitoring
│   │
│   ├── Infrastructure/                    # External dependencies
│   │   ├── TidalSharp/                   # Isolated third-party code
│   │   │   ├── [Direct port from TrevTV] # Minimal modifications
│   │   │   └── TidalSharpExtensions.cs   # Our extensions only
│   │   ├── Adapters/                     # Bridge implementations
│   │   │   ├── TidalCoreAdapter.cs       # ITidalCore → TidalSharp
│   │   │   ├── TidalAuthAdapter.cs       # ITidalAuth → TidalSharp
│   │   │   └── HttpClientAdapter.cs      # IHttpClient bridge
│   │   ├── Resilience/
│   │   │   ├── TidalCircuitBreaker.cs    # Circuit breaker patterns
│   │   │   └── TidalRetryPolicies.cs     # Polly policies
│   │   └── Storage/
│   │       └── JsonTokenStore.cs         # Current JSON storage (future: encrypt)
│   │
│   ├── Integration/                       # Lidarr plugin interfaces  
│   │   ├── TidalIndexer.cs               # Uses shared library patterns
│   │   ├── TidalDownloadClient.cs        # Uses shared library patterns
│   │   ├── TidalSettings.cs              # Extends BaseStreamingSettings
│   │   └── TidalModule.cs                # DI registration
│   │
│   └── Health/                           # Monitoring and diagnostics
│       ├── TidalHealthCheck.cs           # Health check endpoint
│       └── TidalTelemetry.cs             # OpenTelemetry integration
│
├── tests/
│   ├── Tidalarr.Tests.Unit/              # Fast unit tests
│   ├── Tidalarr.Tests.Integration/       # Against real Tidal API
│   └── Tidalarr.Tests.Load/              # Performance testing
│
├── docs/
│   └── adr/                               # Architecture Decision Records
│       ├── 001-adapter-pattern.md
│       ├── 002-shared-library.md
│       ├── 003-token-storage.md
│       ├── 004-circuit-breaker.md
│       └── 005-monitoring-strategy.md
│
└── TidalCLI/                              # Test bed application
```

---

## 3. Core Abstraction Layer (Tech Debt Prevention)

### 3.1 Primary Interfaces

```csharp
// Core/Interfaces/ITidalCore.cs - Abstract TidalSharp away
public interface ITidalCore
{
    Task<TidalTrackInfo> GetTrackAsync(string trackId, CancellationToken cancellationToken = default);
    Task<TidalAlbumInfo> GetAlbumAsync(string albumId, CancellationToken cancellationToken = default);
    Task<TidalSearchResult> SearchAsync(string query, int limit = 100, CancellationToken cancellationToken = default);
    Task<TidalStreamData> GetStreamDataAsync(string trackId, TidalQuality quality, CancellationToken cancellationToken = default);
}

// Core/Interfaces/ITidalAuth.cs - Abstract authentication
public interface ITidalAuth
{
    Task<bool> AuthenticateAsync(TidalCredentials credentials, CancellationToken cancellationToken = default);
    Task<TidalSession> GetValidSessionAsync(CancellationToken cancellationToken = default);
    Task<bool> RefreshSessionAsync(CancellationToken cancellationToken = default);
    bool IsAuthenticated { get; }
}

// Core/Interfaces/ITidalStreamProcessor.cs - Abstract streaming
public interface ITidalStreamProcessor  
{
    Task<Stream> ProcessStreamAsync(TidalStreamData streamData, IProgress<int> progress = null, CancellationToken cancellationToken = default);
    Task<byte[]> ProcessStreamBytesAsync(TidalStreamData streamData, IProgress<int> progress = null, CancellationToken cancellationToken = default);
    Task ApplyMetadataAsync(Stream audioStream, TidalTrackInfo track, CancellationToken cancellationToken = default);
}
```

### 3.2 Adapter Implementation

```csharp
// Infrastructure/Adapters/TidalCoreAdapter.cs
public class TidalCoreAdapter : ITidalCore
{
    private readonly API _tidalApi;                    // TidalSharp dependency isolated here
    private readonly ICircuitBreaker _circuitBreaker;  // Resilience pattern
    private readonly ILogger<TidalCoreAdapter> _logger;
    
    public TidalCoreAdapter(API tidalApi, ICircuitBreaker circuitBreaker, ILogger<TidalCoreAdapter> logger)
    {
        _tidalApi = tidalApi;
        _circuitBreaker = circuitBreaker;
        _logger = logger;
    }
    
    public async Task<TidalTrackInfo> GetTrackAsync(string trackId, CancellationToken cancellationToken = default)
    {
        return await _circuitBreaker.ExecuteAsync(async () =>
        {
            _logger.LogDebug("Fetching track {TrackId}", trackId);
            var tidalResponse = await _tidalApi.GetTrack(trackId, cancellationToken);
            return MapToTidalTrackInfo(tidalResponse); // Our mapping logic
        });
    }
    
    // When TidalSharp needs updating, changes are isolated to this class
}
```

---

## 4. Resilience Patterns with Polly

### 4.1 Circuit Breaker Configuration

```csharp
// Infrastructure/Resilience/TidalCircuitBreaker.cs
public class TidalCircuitBreaker : ICircuitBreaker
{
    private readonly AsyncCircuitBreakerPolicy _policy;
    
    public TidalCircuitBreaker()
    {
        _policy = Policy
            .Handle<HttpRequestException>()
            .Or<TaskCanceledException>()
            .Or<TidalApiException>()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromMinutes(1),
                onBreak: OnCircuitBreak,
                onReset: OnCircuitReset);
    }
    
    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
    {
        return await _policy.ExecuteAsync(operation);
    }
}
```

### 4.2 Comprehensive Retry Policies

```csharp
// Infrastructure/Resilience/TidalRetryPolicies.cs
public static class TidalRetryPolicies
{
    public static readonly AsyncRetryPolicy ApiRetryPolicy = Policy
        .Handle<HttpRequestException>()
        .Or<TidalApiException>(ex => ex.IsRetryable)
        .WaitAndRetryAsync(
            retryCount: 3,
            sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), // Exponential backoff
            onRetry: (exception, timespan, retryCount, context) =>
            {
                Log.Warning("Retry {RetryCount} for {Operation} in {Delay}ms", retryCount, context.OperationKey, timespan.TotalMilliseconds);
            });
    
    public static readonly AsyncRetryPolicy StreamRetryPolicy = Policy
        .Handle<IOException>()
        .Or<TimeoutException>()
        .WaitAndRetryAsync(
            retryCount: 2,
            sleepDurationProvider: _ => TimeSpan.FromSeconds(5),
            onRetry: (exception, timespan, retryCount, context) =>
            {
                Log.Warning("Stream retry {RetryCount}: {Exception}", retryCount, exception.Message);
            });
}
```

---

## 5. Shared Library Integration (74% Code Reduction)

### 5.1 Settings with Shared Library

```csharp
// Integration/TidalSettings.cs - Only 50 lines instead of 200+
public class TidalSettings : BaseStreamingSettings, IIndexerSettings
{
    private static readonly TidalSettingsValidator Validator = new();
    
    // Tidal-specific settings only
    [FieldDefinition(10, Label = "Tidal Market", Type = FieldType.Select,
                     SelectOptions = new[] { "US", "UK", "DE", "FR", "CA", "AU" })]
    public string TidalMarket { get; set; } = "US";
    
    [FieldDefinition(11, Label = "Subscription Tier", Type = FieldType.Select,
                     SelectOptions = new[] { "Free", "Premium", "HiFi", "HiFi Plus" })]
    public TidalSubscriptionTier SubscriptionTier { get; set; } = TidalSubscriptionTier.HiFi;
    
    [FieldDefinition(12, Label = "Include MQA", Type = FieldType.Checkbox,
                     HelpText = "Include Master Quality Authenticated tracks")]
    public bool IncludeMqa { get; set; } = true;
    
    [FieldDefinition(13, Label = "OAuth Redirect URL", Type = FieldType.Textbox,
                     HelpText = "Paste the redirect URL from Tidal OAuth flow")]
    public string RedirectUrl { get; set; }
    
    // Override only for Tidal-specific validation
    public override bool IsValid(out string errorMessage)
    {
        if (!base.IsValid(out errorMessage))
            return false;
            
        return Validator.Validate(this, out errorMessage);
    }
}
```

### 5.2 Indexer with Shared Patterns

```csharp
// Integration/TidalIndexer.cs - Leverage shared library heavily
public class TidalIndexer : HttpIndexerBase<TidalSettings>
{
    private readonly ITidalCore _tidalCore;              // Our abstraction
    private readonly QualityMapper _qualityMapper;       // Shared library
    private readonly IStreamingResponseCache _cache;     // Shared library
    private readonly ICircuitBreaker _circuitBreaker;    // Our resilience
    
    protected override async Task<IList<ReleaseInfo>> FetchReleases(IndexerRequest request)
    {
        // Shared library caching pattern
        var cacheKey = GenerateCacheKey(request.SearchCriteria.SearchTerm);
        if (_cache.TryGet(cacheKey, out IList<ReleaseInfo> cached))
        {
            Logger.LogDebug("Returning cached results for {Query}", request.SearchCriteria.SearchTerm);
            return cached;
        }
        
        // Our abstracted service call
        var results = await _tidalCore.SearchAsync(request.SearchCriteria.SearchTerm);
        
        // Shared library mapping and quality management
        var releases = results.Albums.Select(album => new ReleaseInfo
        {
            Title = album.Title,
            Artist = string.Join(", ", album.Artists),
            DownloadUrl = BuildDownloadUrl(album.Id),
            Quality = _qualityMapper.MapToLidarrQuality(album.AudioQuality), // Shared library
            Size = EstimateSize(album.Duration, album.AudioQuality),
            Categories = new[] { NewznabStandardCategory.Audio },
            PublishDate = album.ReleaseDate
        }).ToList();
        
        // Shared library caching
        _cache.Set(cacheKey, releases, TimeSpan.FromMinutes(Settings.CacheDuration));
        return releases;
    }
}
```

---

## 6. Token Storage Strategy (Deferred Security Enhancement)

### 6.1 Current Approach (Keep TidalSharp Pattern)

```csharp
// Infrastructure/Storage/JsonTokenStore.cs - Keep working pattern for now
public class JsonTokenStore : ITokenStore
{
    // Use TidalSharp's existing JSON storage pattern
    // This works and is proven - don't change for v1
    
    public async Task SaveTokenAsync(TidalUser user)
    {
        // Direct integration with TidalSharp's user storage
        await user.Save(); // Uses existing TidalSharp logic
    }
}
```

### 6.2 Future Security Enhancement (v2)

```csharp
// Core/Interfaces/ISecureTokenStore.cs - Interface ready for future
public interface ISecureTokenStore
{
    Task SaveTokenAsync(string key, OAuthToken token);
    Task<OAuthToken> GetTokenAsync(string key);
    Task DeleteTokenAsync(string key);
}

// Infrastructure/Storage/EncryptedTokenStore.cs - Future implementation
public class EncryptedTokenStore : ISecureTokenStore
{
    // TODO v2: Implement DPAPI/Keychain/libsecret
    // For now, use working JSON pattern from TidalSharp
}
```

**Rationale**: TidalSharp's token storage is proven to work. Changing it in v1 introduces risk without clear benefit. Security enhancement is planned for v2 after core functionality is validated.

---

## 7. Monitoring and Observability

### 7.1 Health Checks

```csharp
// Health/TidalHealthCheck.cs
public class TidalHealthCheck : IHealthCheck
{
    private readonly ITidalAuth _auth;
    private readonly ITidalCore _core;
    
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // Quick API test
            if (!_auth.IsAuthenticated)
                return HealthCheckResult.Degraded("Not authenticated");
                
            // Test basic API connectivity
            await _core.SearchAsync("test", limit: 1, cancellationToken);
            
            return HealthCheckResult.Healthy("Tidal API accessible");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Tidal API error", ex);
        }
    }
}
```

### 7.2 Telemetry Integration

```csharp
// Health/TidalTelemetry.cs
public class TidalTelemetry
{
    private static readonly Counter<int> SearchCounter = 
        Meter.CreateCounter<int>("tidal.searches");
    private static readonly Histogram<double> ApiLatency = 
        Meter.CreateHistogram<double>("tidal.api.duration", "ms");
    
    public static void RecordSearch(string query, double duration)
    {
        SearchCounter.Add(1, new("query.type", GetQueryType(query)));
        ApiLatency.Record(duration, new("operation", "search"));
    }
}
```

---

## 8. Implementation Timeline (3 Weeks)

### **Week 0 (Pre-Development)**
- [x] Set up CI/CD pipeline with integration tests
- [x] Create ADRs for key architectural decisions
- [x] Define interface contracts

### **Week 1: Foundation + Abstraction**
**Day 1-2**: Project structure and NuGet integration
- Add `Lidarr.Plugin.Common` NuGet package
- Set up domain boundaries and folder structure
- Create core interfaces (ITidalCore, ITidalAuth, ITidalStreamProcessor)

**Day 3-4**: TidalSharp port and adapter layer
- Direct port TidalSharp to Infrastructure/TidalSharp/
- Implement adapter classes (TidalCoreAdapter, TidalAuthAdapter)
- Create HTTP client bridge

**Day 5**: Resilience patterns
- Implement circuit breaker with Polly
- Add retry policies and error handling
- Set up basic health checks

### **Week 2: Core Services + Integration**
**Day 1-2**: Application services
- Implement TidalService orchestrator
- Create search and download services
- Add input validation

**Day 3-4**: Lidarr integration
- Implement TidalIndexer with shared library patterns
- Implement TidalDownloadClient with shared library patterns
- Create TidalSettings extending BaseStreamingSettings

**Day 5**: Testing infrastructure
- Set up unit tests with mocking
- Create integration tests against real Tidal API
- Add performance benchmarking

### **Week 3: Polish + Validation**
**Day 1-2**: End-to-end testing
- Complete OAuth flow testing
- Validate search and download functionality
- Test quality detection and streaming

**Day 3-4**: Monitoring and observability
- Add telemetry and metrics
- Implement comprehensive health checks
- Create monitoring dashboards

**Day 5**: Documentation and release prep
- Complete ADRs and documentation
- Package for deployment
- Conduct security review (excluding token encryption for v1)

---

## 9. Architecture Decision Records

### ADR-001: Adapter Pattern Over Direct Integration
**Decision**: Use adapter pattern to isolate TidalSharp from our domain  
**Rationale**: Prevents technical debt, enables testing, allows TidalSharp updates without affecting our code  
**Consequences**: Extra layer of abstraction, but significant maintainability benefits  

### ADR-002: Shared Library Adoption
**Decision**: Use Lidarr.Plugin.Common NuGet package for all Lidarr integration  
**Rationale**: 74% code reduction, proven patterns, professional quality  
**Consequences**: Dependency on external package, but massive development acceleration  

### ADR-003: Token Storage Strategy
**Decision**: Keep TidalSharp JSON storage for v1, plan encryption for v2  
**Rationale**: Working solution reduces v1 risk, security enhancement can be added later  
**Consequences**: Less secure initially, but proven functionality for launch  

### ADR-004: Circuit Breaker Pattern
**Decision**: Use Polly for all external API calls  
**Rationale**: Chief Architect requirement, prevents cascading failures  
**Consequences**: Additional complexity, but essential for production resilience  

### ADR-005: Monitoring Strategy
**Decision**: OpenTelemetry + health checks from day one  
**Rationale**: Observability is essential for production deployment  
**Consequences**: Additional setup, but critical for operational excellence  

---

## 10. Success Metrics (Updated with Chief Architect Feedback)

### **MVP (Must Have)**
- [x] ✅ Abstraction over TidalSharp (tech debt prevention)
- [x] ✅ Circuit breaker for API calls (resilience)
- [x] ✅ Shared library integration (code reduction)
- [ ] 🔄 OAuth authentication working
- [ ] 🔄 Search returns accurate results
- [ ] 🔄 Download functionality working
- [ ] 🔄 Health monitoring endpoint
- [ ] 🔄 Integration test suite

### **V1.0 (Should Have)**  
- [ ] 🔄 Telemetry and metrics (OpenTelemetry)
- [ ] 🔄 Performance benchmarks
- [ ] 🔄 Comprehensive error handling
- [ ] 🔄 Quality tier management
- [ ] 🔄 Load testing validation

### **V2.0 (Could Have)**
- [ ] 🔲 Encrypted token storage (DPAPI/Keychain)
- [ ] 🔲 Feature flags support
- [ ] 🔲 Advanced streaming features (BTS, encryption)
- [ ] 🔲 Batch processing optimizations
- [ ] 🔲 Lyrics integration

---

## 11. Risk Assessment with Mitigation

| Risk | Probability | Impact | Mitigation Strategy |
|------|-------------|--------|-------------------|
| **TidalSharp API Changes** | Medium | High | Adapter pattern isolates changes to infrastructure layer |
| **Shared Library Breaking Changes** | Low | Medium | Pin to specific version, test upgrades in CI |
| **Tidal API Rate Limiting** | High | Medium | Circuit breaker + exponential backoff with Polly |
| **OAuth Flow Issues** | Medium | High | Extensive testing with real accounts, fallback flows |
| **Stream Processing Failures** | Medium | High | Focus on MPD format, BTS as enhancement |
| **Token Security Concerns** | Low | Medium | Keep working pattern for v1, plan encryption for v2 |

---

## Final Validation

**✅ Chief Architect Feedback Incorporated:**
- Abstraction layer prevents technical debt
- Circuit breaker and retry patterns with Polly
- Domain boundaries enforced through structure
- Monitoring and observability built-in
- Token storage security acknowledged and planned

**✅ Shared Library Benefits Maximized:**
- 74% code reduction confirmed by dedicated repository
- Professional NuGet package integration
- Battle-tested patterns from Qobuzarr
- Consistent ecosystem approach

**✅ TidalSharp Integration Validated:**
- Working authentication and streaming logic
- Proven OAuth flow and manifest parsing
- Minimal modifications required
- Clear upgrade path when needed

**Timeline**: 3 weeks with professional quality  
**Tech Debt**: Proactively prevented through architecture  
**Maintainability**: High through abstraction and shared patterns  
**Production Readiness**: Built-in through monitoring and resilience  

This plan balances pragmatism with architectural excellence, delivering a production-ready Tidalarr plugin that will serve as a foundation for the streaming plugin ecosystem.
