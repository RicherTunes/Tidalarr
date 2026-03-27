> **Note:** This document is historical and may not reflect current architecture. It was written during an early iteration of hardening analysis. See CLAUDE.md for current guidance.

# Iteration 1: Edge Cases and Error Scenarios Analysis
## Hardening Tidalarr Against Real-World Failures

---

## Critical Edge Cases Identified

### 1. Authentication Edge Cases

#### **OAuth Flow Failures**
**Scenario**: User closes browser during OAuth, network fails during token exchange, invalid redirect URL
**Current Gap**: Basic error handling only
**Solution**:
```csharp
public class TidalOAuthService
{
    public async Task<TidalAuthResult> HandleOAuthCallbackAsync(string redirectUrl, string expectedState, TimeSpan timeout = default)
    {
        try
        {
            // Validate state parameter to prevent CSRF
            if (!ValidateState(redirectUrl, expectedState))
                return TidalAuthResult.SecurityFailure("Invalid state parameter");
            
            // Extract auth code with validation
            var authCode = ExtractAuthCode(redirectUrl);
            if (string.IsNullOrEmpty(authCode))
                return TidalAuthResult.ParseFailure("Authorization code not found");
            
            // Exchange with timeout and retry
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
            cts.CancelAfter(timeout != default ? timeout : TimeSpan.FromMinutes(2));
            
            var tokens = await ExchangeCodeWithRetry(authCode, maxRetries: 3, cts.Token);
            return TidalAuthResult.Success(tokens);
        }
        catch (OperationCanceledException)
        {
            return TidalAuthResult.TimeoutFailure("OAuth exchange timed out");
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("timeout"))
        {
            return TidalAuthResult.NetworkFailure("Network timeout during authentication");
        }
    }
}
```

#### **Token Refresh Edge Cases**
**Scenario**: Refresh token expired, refresh during concurrent requests, corrupted token storage
**Solution**:
```csharp
public class TidalTokenManager
{
    private readonly SemaphoreSlim _refreshSemaphore = new(1, 1);
    private readonly object _tokenLock = new();
    
    public async Task<TidalTokens> GetValidTokensAsync()
    {
        lock (_tokenLock)
        {
            if (_currentTokens != null && !_currentTokens.IsExpired)
                return _currentTokens;
        }
        
        // Only one thread can refresh at a time
        await _refreshSemaphore.WaitAsync();
        try
        {
            // Double-check pattern after acquiring lock
            lock (_tokenLock)
            {
                if (_currentTokens != null && !_currentTokens.IsExpired)
                    return _currentTokens;
            }
            
            var refreshedTokens = await RefreshTokensWithFallback();
            
            lock (_tokenLock)
            {
                _currentTokens = refreshedTokens;
            }
            
            return refreshedTokens;
        }
        finally
        {
            _refreshSemaphore.Release();
        }
    }
    
    private async Task<TidalTokens> RefreshTokensWithFallback()
    {
        try
        {
            return await _oauthService.RefreshAsync(_currentTokens.RefreshToken);
        }
        catch (TidalAuthException ex) when (ex.IsRefreshTokenExpired)
        {
            // Refresh token expired - need full re-authentication
            throw new TidalReauthenticationRequiredException("Refresh token expired, full login required", ex);
        }
        catch (Exception ex)
        {
            // Fallback: try loading from storage in case memory is corrupted
            var storedTokens = await _storage.LoadTokensAsync();
            if (storedTokens != null && storedTokens.RefreshToken != _currentTokens?.RefreshToken)
            {
                return await _oauthService.RefreshAsync(storedTokens.RefreshToken);
            }
            
            throw new TidalTokenRefreshException("Token refresh failed completely", ex);
        }
    }
}
```

### 2. API Call Edge Cases

#### **Rate Limiting Scenarios**
**Scenario**: 429 responses, temporary IP bans, progressive rate limit increases
**Solution**:
```csharp
public class TidalRateLimitHandler
{
    private static readonly Dictionary<string, DateTime> LastRequestTimes = new();
    private static readonly Dictionary<string, TimeSpan> BackoffDelays = new();
    
    public async Task<T> ExecuteWithRateLimitAsync<T>(Func<Task<T>> operation, string endpoint)
    {
        var backoffKey = GetEndpointCategory(endpoint);
        
        // Progressive backoff per endpoint type
        if (BackoffDelays.TryGetValue(backoffKey, out var delay))
        {
            var timeSinceLastRequest = DateTime.UtcNow - LastRequestTimes.GetValueOrDefault(backoffKey);
            if (timeSinceLastRequest < delay)
            {
                await Task.Delay(delay - timeSinceLastRequest);
            }
        }
        
        LastRequestTimes[backoffKey] = DateTime.UtcNow;
        
        try
        {
            var result = await operation();
            
            // Success - reduce backoff
            if (BackoffDelays.ContainsKey(backoffKey))
                BackoffDelays[backoffKey] = TimeSpan.FromSeconds(Math.Max(1, BackoffDelays[backoffKey].TotalSeconds * 0.8));
            
            return result;
        }
        catch (TidalRateLimitException)
        {
            // Increase backoff exponentially
            var currentBackoff = BackoffDelays.GetValueOrDefault(backoffKey, TimeSpan.FromSeconds(1));
            BackoffDelays[backoffKey] = TimeSpan.FromSeconds(Math.Min(300, currentBackoff.TotalSeconds * 2));
            
            throw;
        }
    }
}
```

#### **Partial Response Edge Cases**
**Scenario**: Missing tracks in album, unavailable qualities, geo-restricted content
**Solution**:
```csharp
public class TidalSearchService
{
    public async Task<TidalSearchResults> SearchWithFallbackAsync(string query)
    {
        var results = await _apiClient.SearchAsync(query);
        
        // Filter out unavailable content
        var availableAlbums = results.Albums
            .Where(album => album.IsAvailable && !album.IsGeoRestricted)
            .Where(album => album.Tracks?.Any() == true) // Has tracks
            .Select(album => ValidateAndSanitizeAlbum(album))
            .Where(album => album != null)
            .ToList();
        
        // If no albums, try track-only search
        if (!availableAlbums.Any() && results.Tracks.Any())
        {
            var availableTracks = results.Tracks
                .Where(track => track.IsAvailable && !track.IsGeoRestricted)
                .Select(track => ConvertTrackToSingleAlbum(track))
                .ToList();
            
            availableAlbums.AddRange(availableTracks);
        }
        
        return new TidalSearchResults { Albums = availableAlbums };
    }
    
    private TidalAlbum ValidateAndSanitizeAlbum(TidalAlbum album)
    {
        // Validate required fields
        if (string.IsNullOrEmpty(album.Title) || !album.Artists.Any())
            return null;
        
        // Sanitize problematic characters
        album.Title = SanitizeTitle(album.Title);
        album.Artists = album.Artists.Select(a => SanitizeArtistName(a)).ToList();
        
        // Filter available qualities only
        album.AvailableQualities = album.AvailableQualities
            .Where(q => IsQualityAccessible(q, _userSubscription))
            .ToList();
        
        return album.AvailableQualities.Any() ? album : null;
    }
}
```

### 3. Streaming Edge Cases

#### **Manifest Processing Failures**
**Scenario**: Corrupted manifests, unsupported formats, missing chunks, network interruptions during download
**Solution**:
```csharp
public class TidalManifestParser
{
    public async Task<TidalManifest> ParseWithValidationAsync(string encodedManifest, string mimeType)
    {
        try
        {
            // Validate base64 encoding
            if (!IsValidBase64(encodedManifest))
                throw new TidalManifestException("Invalid base64 manifest encoding");
            
            var manifestData = Convert.FromBase64String(encodedManifest);
            var decodedManifest = Encoding.UTF8.GetString(manifestData);
            
            // Validate manifest is not empty or corrupted
            if (string.IsNullOrWhiteSpace(decodedManifest) || decodedManifest.Length < 50)
                throw new TidalManifestException("Manifest appears corrupted or empty");
            
            var manifest = mimeType switch
            {
                "application/dash+xml" => await ParseDashManifestWithValidation(decodedManifest),
                "application/vnd.tidal.bts" => ParseBtsManifestWithValidation(decodedManifest),
                _ => throw new UnsupportedManifestException($"Unsupported manifest: {mimeType}")
            };
            
            // Validate parsed manifest
            if (!manifest.ChunkUrls.Any())
                throw new TidalManifestException("No chunk URLs found in manifest");
            
            // Test first chunk URL accessibility
            await ValidateFirstChunkAccessibility(manifest.ChunkUrls[0]);
            
            return manifest;
        }
        catch (Exception ex) when (!(ex is TidalManifestException))
        {
            throw new TidalManifestException($"Failed to parse {mimeType} manifest", ex);
        }
    }
}

public class TidalChunkDownloader
{
    public async Task<Stream> DownloadWithRecoveryAsync(string[] chunkUrls, IProgress<ChunkProgress> progress)
    {
        var downloadedChunks = new Dictionary<int, byte[]>();
        var failedChunks = new List<int>();
        
        // First pass: download all chunks
        for (int i = 0; i < chunkUrls.Length; i++)
        {
            try
            {
                var chunk = await DownloadChunkWithRetry(chunkUrls[i], maxRetries: 3);
                downloadedChunks[i] = chunk;
                progress?.Report(new ChunkProgress(i + 1, chunkUrls.Length, 0));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to download chunk {ChunkIndex}", i);
                failedChunks.Add(i);
            }
        }
        
        // Recovery pass: retry failed chunks with different strategy
        if (failedChunks.Any())
        {
            await Task.Delay(TimeSpan.FromSeconds(2)); // Brief pause
            
            foreach (var chunkIndex in failedChunks)
            {
                try
                {
                    var chunk = await DownloadChunkWithAlternativeStrategy(chunkUrls[chunkIndex]);
                    downloadedChunks[chunkIndex] = chunk;
                    progress?.Report(new ChunkProgress(chunkIndex, chunkUrls.Length, 1));
                }
                catch (Exception ex)
                {
                    // Critical: missing chunks will corrupt the stream
                    throw new TidalChunkDownloadException($"Failed to download critical chunk {chunkIndex}", ex);
                }
            }
        }
        
        // Assemble in correct order - CRITICAL for Tidal streams
        return AssembleChunksInOrder(downloadedChunks, chunkUrls.Length);
    }
}
```

### 4. Storage Edge Cases

#### **Token Storage Corruption & Recovery**
**Solution**:
```csharp
public class JsonTokenStorage
{
    public async Task<TidalTokens> LoadTokensWithRecoveryAsync()
    {
        var primaryPath = GetPrimaryStoragePath();
        var backupPath = GetBackupStoragePath();
        
        // Try primary storage
        if (File.Exists(primaryPath))
        {
            try
            {
                var tokens = await LoadFromPath(primaryPath);
                if (ValidateTokens(tokens))
                {
                    // Create backup of working tokens
                    await CreateBackupAsync(primaryPath, backupPath);
                    return tokens;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Primary token storage corrupted, trying backup");
            }
        }
        
        // Try backup storage
        if (File.Exists(backupPath))
        {
            try
            {
                var tokens = await LoadFromPath(backupPath);
                if (ValidateTokens(tokens))
                {
                    // Restore primary from backup
                    await File.CopyAsync(backupPath, primaryPath);
                    return tokens;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Both primary and backup token storage corrupted");
            }
        }
        
        throw new TidalAuthException("No valid token storage found - re-authentication required");
    }
}
```

### 5. Quality Detection Edge Cases

#### **Regional Quality Variations**
**Scenario**: HiRes available in US but not EU, MQA subscription tier changes
**Solution**:
```csharp
public class TidalQualityDetector
{
    public async Task<List<TidalQuality>> DetectAvailableQualitiesAsync(string trackId, string market, TidalSubscriptionTier subscription)
    {
        var qualities = new List<TidalQuality>();
        
        // Test each quality tier with actual API calls
        var qualityTests = new[]
        {
            (TidalQuality.HiRes, CanAccessHiRes(subscription, market)),
            (TidalQuality.Lossless, CanAccessLossless(subscription)),
            (TidalQuality.High, true), // Always available
            (TidalQuality.Low, true)   // Always available
        };
        
        foreach (var (quality, hasAccess) in qualityTests)
        {
            if (!hasAccess) continue;
            
            try
            {
                // Test actual API endpoint for this quality
                var streamInfo = await _apiClient.GetStreamInfoAsync(trackId, quality);
                if (streamInfo != null && streamInfo.ChunkUrls.Any())
                {
                    qualities.Add(quality);
                }
            }
            catch (TidalQualityUnavailableException)
            {
                // Expected for unavailable qualities
                continue;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to test quality {Quality} for track {TrackId}", quality, trackId);
            }
        }
        
        return qualities.Any() ? qualities : new List<TidalQuality> { TidalQuality.High }; // Fallback
    }
}
```

### 6. Search Edge Cases

#### **Search Result Inconsistencies**
**Scenario**: Empty results, malformed responses, duplicate entries, missing metadata
**Solution**:
```csharp
public class TidalSearchResponseValidator
{
    public TidalSearchResults ValidateAndCleanResults(TidalSearchResults rawResults)
    {
        var cleanedAlbums = rawResults.Albums
            .Where(album => IsValidAlbum(album))
            .Select(album => CleanAlbumMetadata(album))
            .GroupBy(album => album.Id)
            .Select(group => group.First()) // Remove duplicates
            .Where(album => album.Tracks?.Any() == true)
            .ToList();
        
        var cleanedTracks = rawResults.Tracks
            .Where(track => IsValidTrack(track))
            .Select(track => CleanTrackMetadata(track))
            .GroupBy(track => track.Id)
            .Select(group => group.First())
            .ToList();
        
        return new TidalSearchResults
        {
            Albums = cleanedAlbums,
            Tracks = cleanedTracks,
            ResultCount = cleanedAlbums.Count + cleanedTracks.Count,
            HasMore = rawResults.HasMore && cleanedAlbums.Count < rawResults.Albums.Count
        };
    }
    
    private bool IsValidAlbum(TidalAlbum album)
    {
        return !string.IsNullOrWhiteSpace(album.Id) &&
               !string.IsNullOrWhiteSpace(album.Title) &&
               album.Artists?.Any() == true &&
               album.ReleaseDate != default &&
               album.Title.Length <= 500; // Prevent extremely long titles
    }
}
```

### 7. Network and Infrastructure Edge Cases

#### **Connection Resilience**
**Solution**:
```csharp
public class TidalNetworkResiliencePolicy
{
    public static AsyncRetryPolicy CreateRetryPolicy(string operationType)
    {
        return Policy
            .Handle<HttpRequestException>()
            .Or<TaskCanceledException>()
            .Or<SocketException>()
            .Or<TidalRateLimitException>()
            .WaitAndRetryAsync(
                retryCount: GetRetryCount(operationType),
                sleepDurationProvider: retryAttempt => CalculateDelay(operationType, retryAttempt),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    TidalTelemetry.RecordRetry(operationType, retryCount, outcome.Exception?.GetType().Name);
                }
            );
    }
    
    private static int GetRetryCount(string operationType) => operationType switch
    {
        "authentication" => 5,  // Critical - higher retries
        "search" => 3,         // Normal retries
        "chunk_download" => 2, // Low retries - chunks fail fast
        _ => 3
    };
}
```

#### **Memory Management for Large Downloads**
**Solution**:
```csharp
public class TidalStreamingDownloader
{
    private readonly ArrayPool<byte> _bufferPool;
    
    public async Task<Stream> DownloadLargeStreamAsync(string[] chunkUrls, long estimatedSize)
    {
        // Use temp file for large streams to prevent memory issues
        if (estimatedSize > 100_000_000) // > 100MB
        {
            return await DownloadToTempFileAsync(chunkUrls);
        }
        
        // Use memory stream for smaller files
        return await DownloadToMemoryAsync(chunkUrls);
    }
    
    private async Task<FileStream> DownloadToTempFileAsync(string[] chunkUrls)
    {
        var tempPath = Path.GetTempFileName();
        var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 65536, FileOptions.DeleteOnClose);
        
        try
        {
            foreach (var url in chunkUrls)
            {
                using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                using var contentStream = await response.Content.ReadAsStreamAsync();
                await contentStream.CopyToAsync(fileStream);
            }
            
            fileStream.Seek(0, SeekOrigin.Begin);
            return fileStream;
        }
        catch
        {
            fileStream?.Dispose();
            throw;
        }
    }
}
```

---

## Additional Edge Cases to Handle

### Configuration Edge Cases
- **Invalid market codes**: Fallback to "US" with warning
- **Subscription tier mismatches**: Graceful quality downgrade
- **Missing configuration**: Sensible defaults with clear error messages

### Metadata Edge Cases  
- **Special characters in titles**: Proper Unicode handling and file name sanitization
- **Missing cover art**: Fallback to default or skip without failure
- **Extremely long metadata**: Truncation with preservation of essential information

### Concurrent Usage Edge Cases
- **Multiple simultaneous downloads**: Resource management and throttling
- **Shared authentication state**: Thread-safe token access
- **Cache contention**: Thread-safe caching with proper locking

### File System Edge Cases
- **Disk space exhaustion**: Pre-download space checks and cleanup
- **Permission issues**: Clear error messages and fallback strategies  
- **Long file paths**: Path length validation and truncation on Windows

This comprehensive edge case analysis ensures Tidalarr will handle real-world usage scenarios gracefully rather than failing catastrophically.
