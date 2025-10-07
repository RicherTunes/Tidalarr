# Iteration 3: Shared Library Improvements and Contributions
## How Tidalarr Can Enhance the Ecosystem

---

## Strategic Opportunities to Improve Lidarr.Plugin.Common

### 1. OAuth 2.0 Framework Enhancement

#### **Current Gap**: Shared library has basic auth, but no OAuth 2.0 PKCE framework
**Tidalarr Contribution**: Complete OAuth 2.0 framework for streaming services

```csharp
// Proposed addition to Lidarr.Plugin.Common
namespace Lidarr.Plugin.Common.Authentication.OAuth
{
    public abstract class BaseOAuthService<TTokens> where TTokens : class, IOAuthTokens
    {
        protected abstract string ClientId { get; }
        protected abstract string ClientSecret { get; }
        protected abstract string RedirectUri { get; }
        protected abstract string AuthorizationEndpoint { get; }
        protected abstract string TokenEndpoint { get; }
        protected abstract string[] Scopes { get; }
        
        public virtual async Task<OAuthAuthorizationUrl> GenerateAuthorizationUrlAsync()
        {
            var (verifier, challenge) = _pkceGenerator.GeneratePair();
            var state = GenerateSecureState();
            
            var authUrl = BuildAuthorizationUrl(challenge, state);
            
            // Store PKCE data securely for callback
            await _pkceStorage.StoreAsync(state, verifier, TimeSpan.FromMinutes(10));
            
            return new OAuthAuthorizationUrl(authUrl, state);
        }
        
        public virtual async Task<TTokens> HandleCallbackAsync(string callbackUrl)
        {
            var (authCode, state) = ParseCallback(callbackUrl);
            var codeVerifier = await _pkceStorage.RetrieveAsync(state);
            
            return await ExchangeCodeForTokensAsync(authCode, codeVerifier);
        }
        
        protected abstract Task<TTokens> ExchangeCodeForTokensAsync(string authCode, string codeVerifier);
        protected abstract Task<TTokens> RefreshTokensAsync(string refreshToken);
    }
    
    public class PKCEGenerator
    {
        public (string codeVerifier, string codeChallenge) GeneratePair()
        {
            var codeVerifier = GenerateCodeVerifier(128);
            using var sha256 = SHA256.Create();
            var challengeBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(codeVerifier));
            var codeChallenge = Base64UrlEncode(challengeBytes);
            return (codeVerifier, codeChallenge);
        }
    }
}

// Tidalarr implementation would then be:
public class TidalOAuthService : BaseOAuthService<TidalTokens>
{
    protected override string ClientId => TidalConstants.CLIENT_ID_PKCE;
    protected override string ClientSecret => TidalConstants.CLIENT_SECRET_PKCE;
    // ... only Tidal-specific values
}
```

**Ecosystem Benefit**: Spotify, Apple Music, Amazon Music plugins can reuse OAuth framework

### 2. Advanced Caching Framework

#### **Current Gap**: Basic response cache, no intelligence or optimization
**Tidalarr Contribution**: Smart caching with usage patterns and performance optimization

```csharp
// Proposed addition to Lidarr.Plugin.Common.Caching
public class IntelligentStreamingCache : IStreamingResponseCache
{
    private readonly Dictionary<string, CacheEntry> _cache = new();
    private readonly ConcurrentDictionary<string, AccessPattern> _accessPatterns = new();
    
    public async Task<T> GetOrAddWithPatternsAsync<T>(
        string key, 
        Func<Task<T>> factory, 
        StreamingCacheContext context)
    {
        // Track access patterns
        RecordAccess(key, context);
        
        if (TryGetFromIntelligentCache(key, context, out T cachedValue))
            return cachedValue;
        
        var value = await factory();
        
        // Calculate intelligent TTL based on:
        // - Data type (search vs metadata vs stream URLs)
        // - Access frequency
        // - Time of day patterns
        // - Quality tier (higher quality = longer cache)
        var smartTtl = CalculateIntelligentTTL(key, context, _accessPatterns[key]);
        
        SetWithIntelligentEviction(key, value, smartTtl, context);
        return value;
    }
    
    private TimeSpan CalculateIntelligentTTL(string key, StreamingCacheContext context, AccessPattern pattern)
    {
        var baseTtl = context.DataType switch
        {
            StreamingDataType.SearchResults => TimeSpan.FromMinutes(5),
            StreamingDataType.AlbumMetadata => TimeSpan.FromHours(2),
            StreamingDataType.TrackMetadata => TimeSpan.FromHours(6),
            StreamingDataType.StreamUrls => TimeSpan.FromMinutes(30),
            StreamingDataType.CoverArt => TimeSpan.FromDays(1),
            _ => TimeSpan.FromMinutes(10)
        };
        
        // Increase TTL for frequently accessed items
        var frequencyMultiplier = Math.Min(3.0, 1.0 + (pattern.AccessCount * 0.1));
        
        // Increase TTL for higher quality content (more expensive to regenerate)
        var qualityMultiplier = context.Quality switch
        {
            StreamingQualityTier.HiRes => 2.0,
            StreamingQualityTier.Lossless => 1.5,
            _ => 1.0
        };
        
        // Time-of-day adjustment (longer cache during peak hours)
        var timeMultiplier = IsOffPeakHours() ? 0.7 : 1.3;
        
        return TimeSpan.FromTicks((long)(baseTtl.Ticks * frequencyMultiplier * qualityMultiplier * timeMultiplier));
    }
}
```

### 3. Streaming Service Abstraction Framework

#### **Current Gap**: Each service implements streaming differently  
**Tidalarr Contribution**: Universal streaming abstraction

```csharp
// Proposed addition to Lidarr.Plugin.Common.Streaming
public interface IStreamingContentProvider
{
    Task<StreamingContentInfo> GetContentInfoAsync(string contentId, StreamingQualityTier preferredQuality);
    Task<StreamingManifest> GetStreamManifestAsync(string contentId, StreamingQualityTier quality);
    IAsyncEnumerable<StreamingChunk> GetContentChunksAsync(StreamingManifest manifest, IProgress<StreamingProgress> progress);
}

public abstract class BaseStreamingContentProvider : IStreamingContentProvider
{
    protected abstract Task<ServiceSpecificContentInfo> FetchContentInfoAsync(string contentId);
    protected abstract Task<ServiceSpecificManifest> FetchManifestAsync(string contentId, StreamingQualityTier quality);
    protected abstract IAsyncEnumerable<byte[]> DownloadChunksAsync(ServiceSpecificManifest manifest);
    
    // Common logic for quality selection, error handling, progress reporting
    public async Task<StreamingContentInfo> GetContentInfoAsync(string contentId, StreamingQualityTier preferredQuality)
    {
        var serviceInfo = await FetchContentInfoAsync(contentId);
        
        // Universal quality selection logic
        var availableQualities = MapServiceQualities(serviceInfo.AvailableQualities);
        var selectedQuality = SelectBestQuality(availableQualities, preferredQuality);
        
        return new StreamingContentInfo
        {
            ContentId = contentId,
            Title = serviceInfo.Title,
            AvailableQualities = availableQualities,
            SelectedQuality = selectedQuality,
            EstimatedSize = EstimateSize(serviceInfo.Duration, selectedQuality),
            IsAvailable = serviceInfo.IsAvailable
        };
    }
}

// Tidalarr implementation
public class TidalContentProvider : BaseStreamingContentProvider
{
    protected override async Task<TidalContentInfo> FetchContentInfoAsync(string contentId)
    {
        // Tidal-specific implementation
    }
    
    // Only implement service-specific logic, get all common functionality free
}
```

### 4. Universal Quality Management Enhancement

#### **Current State**: Basic quality mapping  
**Tidalarr Enhancement**: Cross-service quality comparison and optimization

```csharp
// Enhancement to existing QualityMapper
public class EnhancedQualityMapper : QualityMapper
{
    // Add quality comparison across services
    public class QualityComparisonResult
    {
        public StreamingQualityTier UniversalTier { get; set; }
        public Dictionary<string, string> ServiceSpecificQualities { get; set; } // Qobuz: "27", Tidal: "HI_RES_LOSSLESS"
        public QualityMetadata Metadata { get; set; }
    }
    
    public QualityComparisonResult CompareQualitiesAcrossServices(
        Dictionary<string, object> serviceQualities, 
        StreamingQualityTier targetQuality)
    {
        var comparison = new QualityComparisonResult
        {
            ServiceSpecificQualities = new Dictionary<string, string>()
        };
        
        // Map each service's quality to universal tier
        foreach (var (serviceName, qualityValue) in serviceQualities)
        {
            var universalQuality = MapServiceQualityToUniversal(serviceName, qualityValue);
            comparison.ServiceSpecificQualities[serviceName] = qualityValue.ToString();
            
            // Find the closest match to target quality
            if (IsCloserToTarget(universalQuality, targetQuality, comparison.UniversalTier))
            {
                comparison.UniversalTier = universalQuality;
                comparison.Metadata = GetQualityMetadata(serviceName, universalQuality);
            }
        }
        
        return comparison;
    }
    
    public class QualityMetadata
    {
        public string BitRate { get; set; }          // "1411 kbps"
        public string SampleRate { get; set; }       // "44.1 kHz" 
        public string BitDepth { get; set; }         // "16-bit"
        public string Format { get; set; }          // "FLAC"
        public bool IsLossless { get; set; }
        public bool IsMasterQuality { get; set; }
    }
}
```

### 5. Plugin Performance Analytics Framework

#### **Contribution**: Universal performance monitoring for all streaming plugins

```csharp
// Addition to Lidarr.Plugin.Common.Analytics
public class StreamingPluginAnalytics
{
    public class PerformanceMetrics
    {
        public TimeSpan AverageSearchTime { get; set; }
        public TimeSpan AverageDownloadTime { get; set; }
        public double SuccessRate { get; set; }
        public int ApiCallsPerOperation { get; set; }
        public Dictionary<StreamingQualityTier, TimeSpan> DownloadTimesByQuality { get; set; }
    }
    
    public async Task<PerformanceMetrics> GeneratePluginPerformanceReportAsync(TimeSpan period)
    {
        var metrics = await CollectMetricsAsync(period);
        
        return new PerformanceMetrics
        {
            AverageSearchTime = CalculateAverageSearchTime(metrics),
            AverageDownloadTime = CalculateAverageDownloadTime(metrics),
            SuccessRate = CalculateSuccessRate(metrics),
            ApiCallsPerOperation = CalculateApiEfficiency(metrics),
            DownloadTimesByQuality = CalculateQualityPerformance(metrics)
        };
    }
    
    // Cross-plugin benchmarking
    public async Task<PluginComparisonReport> ComparePuginPerformanceAsync()
    {
        var qobuzMetrics = await GetPluginMetrics("Qobuzarr");
        var tidalMetrics = await GetPluginMetrics("Tidalarr");
        var spotifyMetrics = await GetPluginMetrics("Spotifyarr"); // Future
        
        return GenerateComparisonReport(qobuzMetrics, tidalMetrics, spotifyMetrics);
    }
}
```

### 6. Advanced Download Orchestration Framework

#### **Contribution**: Universal download patterns for all streaming services

```csharp
// Addition to Lidarr.Plugin.Common.Download
public class UniversalDownloadOrchestrator
{
    public async Task<DownloadResult> OrchestrateBatchDownloadAsync(
        IEnumerable<DownloadRequest> requests,
        DownloadStrategy strategy,
        IProgress<BatchDownloadProgress> progress)
    {
        var requestGroups = strategy switch
        {
            DownloadStrategy.Sequential => requests.Chunk(1),
            DownloadStrategy.Parallel => requests.Chunk(Environment.ProcessorCount),
            DownloadStrategy.Adaptive => GroupByOptimalBatchSize(requests),
            _ => requests.Chunk(3)
        };
        
        var allResults = new List<DownloadResult>();
        
        foreach (var (group, groupIndex) in requestGroups.Select((g, i) => (g, i)))
        {
            // Resource availability check before each group
            await EnsureResourcesForGroupAsync(group);
            
            // Process group with appropriate concurrency
            var groupTasks = group.Select(request => ProcessSingleDownloadAsync(request, strategy));
            var groupResults = await Task.WhenAll(groupTasks);
            
            allResults.AddRange(groupResults);
            
            // Report progress
            progress?.Report(new BatchDownloadProgress(allResults.Count, requests.Count()));
            
            // Adaptive delay between groups based on system performance
            await CalculateOptimalDelayAsync(groupResults, strategy);
        }
        
        return AggregateResults(allResults);
    }
    
    private async Task<TimeSpan> CalculateOptimalDelayAsync(DownloadResult[] groupResults, DownloadStrategy strategy)
    {
        var avgResponseTime = groupResults.Average(r => r.Duration.TotalMilliseconds);
        var errorRate = groupResults.Count(r => !r.Success) / (double)groupResults.Length;
        
        // Adaptive delay based on performance
        if (errorRate > 0.2) return TimeSpan.FromSeconds(5); // High error rate - slow down
        if (avgResponseTime > 10000) return TimeSpan.FromSeconds(3); // Slow responses
        if (errorRate < 0.05 && avgResponseTime < 2000) return TimeSpan.FromMilliseconds(500); // Performing well
        
        return TimeSpan.FromSeconds(2); // Default
    }
}
```

### 7. Cross-Service Quality Standardization

#### **Enhancement**: Universal quality detection and mapping

```csharp
// Enhancement to existing QualityMapper
public class UniversalQualityAnalyzer
{
    public class QualityAnalysisResult
    {
        public StreamingQualityTier RecommendedTier { get; set; }
        public Dictionary<string, QualityAvailability> ServiceAvailability { get; set; }
        public QualityRecommendation UserRecommendation { get; set; }
    }
    
    public async Task<QualityAnalysisResult> AnalyzeOptimalQualityAsync(
        string contentId, 
        UserPreferences preferences,
        Dictionary<string, IStreamingContentProvider> availableServices)
    {
        var serviceQualities = new Dictionary<string, QualityAvailability>();
        
        // Test quality availability across all services
        await foreach (var (serviceName, provider) in availableServices.ToAsyncEnumerable())
        {
            try
            {
                var contentInfo = await provider.GetContentInfoAsync(contentId, preferences.PreferredQuality);
                serviceQualities[serviceName] = new QualityAvailability
                {
                    AvailableQualities = contentInfo.AvailableQualities,
                    RecommendedQuality = contentInfo.SelectedQuality,
                    SupportsLossless = contentInfo.AvailableQualities.Any(q => q >= StreamingQualityTier.Lossless),
                    EstimatedBitrate = EstimateBitrate(contentInfo.SelectedQuality)
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to check quality for {ServiceName}", serviceName);
                serviceQualities[serviceName] = QualityAvailability.Unavailable();
            }
        }
        
        // Generate cross-service recommendation
        var recommendation = GenerateQualityRecommendation(serviceQualities, preferences);
        
        return new QualityAnalysisResult
        {
            RecommendedTier = recommendation.OptimalTier,
            ServiceAvailability = serviceQualities,
            UserRecommendation = recommendation
        };
    }
}
```

### 8. Universal Metadata Framework

#### **Enhancement**: Standardized metadata across all streaming services

```csharp
// Addition to Lidarr.Plugin.Common.Metadata
public class UniversalMetadataAggregator
{
    public async Task<EnrichedStreamingMetadata> AggregateMetadataAsync(
        string contentId,
        IEnumerable<IMetadataProvider> providers)
    {
        var metadataResults = new List<StreamingMetadata>();
        
        // Collect metadata from all available providers
        await foreach (var provider in providers.ToAsyncEnumerable())
        {
            try
            {
                var metadata = await provider.GetMetadataAsync(contentId);
                if (metadata != null)
                    metadataResults.Add(metadata);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Provider {ProviderName} failed for content {ContentId}", 
                    provider.GetType().Name, contentId);
            }
        }
        
        // Merge metadata with conflict resolution
        return MergeMetadataWithConflictResolution(metadataResults);
    }
    
    private EnrichedStreamingMetadata MergeMetadataWithConflictResolution(List<StreamingMetadata> metadataList)
    {
        var merged = new EnrichedStreamingMetadata();
        
        // Priority-based merging (some services have more accurate data)
        var priorityOrder = new[] { "MusicBrainz", "Tidal", "Qobuz", "Spotify" };
        
        foreach (var provider in priorityOrder)
        {
            var metadata = metadataList.FirstOrDefault(m => m.ProviderName == provider);
            if (metadata == null) continue;
            
            // Merge with conflict resolution rules
            merged.Title ??= metadata.Title;
            merged.Artists = merged.Artists?.Any() == true ? merged.Artists : metadata.Artists;
            merged.ReleaseDate = merged.ReleaseDate == default ? metadata.ReleaseDate : merged.ReleaseDate;
            merged.Genres = MergeGenres(merged.Genres, metadata.Genres);
            
            // Always prefer higher resolution cover art
            if (metadata.CoverArt?.Resolution > merged.CoverArt?.Resolution)
                merged.CoverArt = metadata.CoverArt;
        }
        
        // Validate merged result
        return ValidateAndSanitizeMetadata(merged);
    }
}
```

### 9. Advanced Health Monitoring Framework

#### **Contribution**: Comprehensive health monitoring for streaming ecosystem

```csharp
// Addition to Lidarr.Plugin.Common.Health
public class StreamingEcosystemHealthMonitor
{
    public async Task<EcosystemHealthReport> GenerateEcosystemReportAsync()
    {
        var pluginHealthChecks = await Task.WhenAll(
            CheckPluginHealth("Qobuzarr"),
            CheckPluginHealth("Tidalarr"),
            CheckPluginHealth("Spotifyarr") // Future plugins
        );
        
        var apiHealthChecks = await Task.WhenAll(
            CheckServiceApiHealth("Qobuz"),
            CheckServiceApiHealth("Tidal"),
            CheckServiceApiHealth("Spotify") // Future
        );
        
        return new EcosystemHealthReport
        {
            PluginHealth = pluginHealthChecks.ToDictionary(h => h.PluginName, h => h.Status),
            ApiHealth = apiHealthChecks.ToDictionary(h => h.ServiceName, h => h.Status),
            OverallStatus = CalculateOverallHealth(pluginHealthChecks, apiHealthChecks),
            Recommendations = GenerateHealthRecommendations(pluginHealthChecks, apiHealthChecks),
            Timestamp = DateTime.UtcNow
        };
    }
    
    public class PluginHealthStatus
    {
        public string PluginName { get; set; }
        public HealthStatus Status { get; set; }
        public TimeSpan ResponseTime { get; set; }
        public string Version { get; set; }
        public List<HealthIssue> Issues { get; set; } = new();
        public Dictionary<string, object> Metrics { get; set; } = new();
    }
}
```

### 10. Plugin Development Framework

#### **Contribution**: Streamlined development tools and templates

```csharp
// Addition to Lidarr.Plugin.Common.Development
public class StreamingPluginGenerator
{
    public async Task<GeneratedPluginStructure> GeneratePluginAsync(PluginGenerationRequest request)
    {
        var template = await LoadPluginTemplate(request.ServiceType);
        
        // Generate complete plugin structure
        var structure = new GeneratedPluginStructure
        {
            ProjectStructure = GenerateProjectStructure(request),
            SettingsClass = GenerateSettingsClass(request),
            IndexerClass = GenerateIndexerClass(request),
            DownloadClientClass = GenerateDownloadClientClass(request),
            AuthenticationService = GenerateAuthService(request),
            ApiClient = GenerateApiClient(request),
            Models = GenerateModels(request),
            Tests = GenerateTestStructure(request),
            Documentation = GenerateDocumentation(request)
        };
        
        return structure;
    }
    
    // Example: Generate new plugin for Amazon Music in 10 minutes instead of 3 weeks
    public async Task CreateAmazonMusicPluginAsync(string outputPath)
    {
        var request = new PluginGenerationRequest
        {
            PluginName = "Amazonarr",
            ServiceName = "Amazon Music",
            AuthenticationType = AuthType.OAuth2,
            QualityTiers = new[] { "SD", "HD", "Ultra HD" },
            ApiEndpoints = await DiscoverApiEndpoints("music.amazon.com"),
            StreamingProtocol = StreamingProtocol.Progressive // vs Chunked for Tidal
        };
        
        var generated = await GeneratePluginAsync(request);
        await WriteToFileSystemAsync(outputPath, generated);
        
        // Result: Working plugin template ready for customization
    }
}
```

---

## Tidalarr's Contributions to Ecosystem

### **Immediate Contributions (Week 4)**
1. **OAuth 2.0 Framework** - Complete PKCE implementation
2. **Chunked Streaming Framework** - For services using segmented delivery  
3. **Advanced Quality Detection** - Cross-service quality comparison
4. **Performance Analytics** - Plugin benchmarking and optimization

### **Future Contributions (Post-V1)**
1. **Universal Metadata Aggregation** - Multi-source metadata merging
2. **Plugin Generation Framework** - Rapid new service integration
3. **Ecosystem Health Monitoring** - Cross-plugin monitoring and alerting
4. **Advanced Caching Intelligence** - AI-powered cache optimization

### **Community Benefits**
- **Faster plugin development**: New services in days, not weeks
- **Higher quality plugins**: Battle-tested patterns and frameworks  
- **Consistent user experience**: Standardized behavior across all services
- **Easier maintenance**: Shared improvements benefit everyone
- **Professional ecosystem**: Enterprise-grade streaming integration

---

## ROI Analysis for Shared Library Investment

### **Development Time Savings**
- **Next plugin (Spotifyarr)**: 3 days instead of 3 weeks (93% reduction)
- **Plugin maintenance**: 70% reduction in ongoing effort
- **Feature additions**: Shared improvements benefit all plugins
- **Bug fixes**: Fix once, deploy everywhere

### **Quality Improvements**  
- **Consistent error handling** across ecosystem
- **Shared security practices** 
- **Performance optimization** benefits all plugins
- **Professional monitoring** and analytics

### **Community Impact**
- **Lower barrier to entry** for new contributors
- **Standardized patterns** make plugins predictable
- **Shared knowledge base** through common framework
- **Ecosystem growth** through reduced development friction

The investment in enhancing the shared library through Tidalarr development creates exponential returns for the entire streaming plugin ecosystem.
