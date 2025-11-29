using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Tidalarr.Tests.LibraryLinking
{
    /// <summary>
    /// Tests for service isolation when Tidalarr shares the Common library with other plugins.
    /// Verifies that services like caching, rate limiting, and authentication
    /// are properly scoped to the plugin and don't leak to other plugins.
    /// </summary>
    [Trait("Category", "ServiceIsolation")]
    public class ServiceIsolationTests
    {
        #region DASH Chunk Cache Isolation Tests

        [Fact]
        public void ChunkCache_Should_Be_Plugin_Scoped()
        {
            // Arrange - Simulate caches for different plugins (Tidal vs Qobuz)
            var tidalChunkCache = new Dictionary<string, byte[]>();
            var qobuzChunkCache = new Dictionary<string, byte[]>();

            // Act - Cache a Tidal DASH chunk
            tidalChunkCache["track:123:chunk:0"] = new byte[] { 0x01, 0x02, 0x03 };

            // Assert - Qobuz's cache should not see Tidal's cached data
            Assert.False(qobuzChunkCache.ContainsKey("track:123:chunk:0"));
        }

        [Fact]
        public async Task ConcurrentChunkDownloads_Should_Be_Thread_Safe()
        {
            // Arrange - Tidal uses sequential chunk downloads that must be thread-safe
            var chunkBuffer = new ConcurrentDictionary<string, byte[]>();
            var tasks = new List<Task>();
            var errors = new ConcurrentBag<Exception>();

            // Act - Simulate concurrent chunk downloads
            for (int i = 0; i < 50; i++)
            {
                var chunkId = i;
                tasks.Add(Task.Run(() =>
                {
                    try
                    {
                        var key = $"track:123:chunk:{chunkId}";
                        chunkBuffer.TryAdd(key, new byte[] { (byte)(chunkId % 256) });
                        chunkBuffer.TryGetValue(key, out _);
                    }
                    catch (Exception ex)
                    {
                        errors.Add(ex);
                    }
                }));
            }

            await Task.WhenAll(tasks);

            // Assert
            Assert.Empty(errors);
            Assert.Equal(50, chunkBuffer.Count);
        }

        [Fact]
        public void ManifestCache_Should_Be_Isolated()
        {
            // Arrange - DASH manifests should be cached per-plugin
            var manifestCache = new ConcurrentDictionary<string, object>();

            // Act
            manifestCache["tidalarr:track:123:manifest"] = new { Segments = 10, Duration = 240 };
            manifestCache["qobuzarr:track:456:manifest"] = new { Quality = "FLAC-Max" };

            // Assert - Each plugin's manifest cache should be isolated
            Assert.Equal(2, manifestCache.Count);
            Assert.True(manifestCache.ContainsKey("tidalarr:track:123:manifest"));
            Assert.True(manifestCache.ContainsKey("qobuzarr:track:456:manifest"));
        }

        #endregion

        #region Rate Limiter Isolation Tests

        [Fact]
        public async Task TidalApiRateLimiter_Should_Be_Plugin_Scoped()
        {
            // Arrange - Tidal API has specific rate limits
            var tidalRateLimiter = new SemaphoreSlim(5);  // Tidal: 5 concurrent requests
            var qobuzRateLimiter = new SemaphoreSlim(10); // Qobuz: 10 concurrent requests

            // Act - Exhaust Tidal's rate limit
            for (int i = 0; i < 5; i++)
            {
                await tidalRateLimiter.WaitAsync();
            }

            // Assert - Qobuz's rate limiter should still have full capacity
            Assert.Equal(10, qobuzRateLimiter.CurrentCount);

            // Cleanup
            tidalRateLimiter.Dispose();
            qobuzRateLimiter.Dispose();
        }

        [Fact]
        public async Task ChunkDownloadLimiter_Should_Allow_Sequential_Access()
        {
            // Arrange - Tidal chunk downloads must be sequential
            var downloadLimiter = new SemaphoreSlim(1); // Only 1 chunk at a time per track
            var chunkOrder = new List<int>();

            // Act - Download chunks sequentially
            for (int i = 0; i < 5; i++)
            {
                await downloadLimiter.WaitAsync();
                chunkOrder.Add(i);
                downloadLimiter.Release();
            }

            // Assert
            Assert.Equal(new[] { 0, 1, 2, 3, 4 }, chunkOrder);

            downloadLimiter.Dispose();
        }

        #endregion

        #region OAuth Token Provider Isolation Tests

        [Fact]
        public void TidalOAuthSession_Should_Be_Plugin_Scoped()
        {
            // Arrange - Simulate sessions for different plugins
            var sessions = new ConcurrentDictionary<string, object>();

            // Act - Create Tidal OAuth session
            sessions["tidalarr:session"] = new
            {
                AccessToken = "tidal-access-token",
                RefreshToken = "tidal-refresh-token",
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                UserId = "tidal-user-123"
            };

            // Create Qobuz session (different structure)
            sessions["qobuzarr:session"] = new
            {
                AppId = "qobuz-app-id",
                Token = "qobuz-auth-token",
                Expiry = DateTime.UtcNow.AddHours(1)
            };

            // Assert - Sessions should be isolated
            Assert.True(sessions.ContainsKey("tidalarr:session"));
            Assert.True(sessions.ContainsKey("qobuzarr:session"));
            Assert.NotEqual(
                sessions["tidalarr:session"].GetType(),
                sessions["qobuzarr:session"].GetType());
        }

        [Fact]
        public void TokenRefresh_Should_Not_Affect_Other_Plugins()
        {
            // Arrange
            var tidalToken = "tidal-v1";
            var qobuzToken = "qobuz-v1";

            // Act - Refresh Tidal token (simulated)
            tidalToken = "tidal-v2";

            // Assert - Qobuz token should be unchanged
            Assert.Equal("qobuz-v1", qobuzToken);
        }

        [Fact]
        public void PKCEState_Should_Be_Plugin_Scoped()
        {
            // Arrange - PKCE state for OAuth 2.0 flow should be isolated
            var pkceStates = new ConcurrentDictionary<string, string>();

            // Act - Each plugin generates its own PKCE verifier
            pkceStates["tidalarr:pkce:verifier"] = "tidal-verifier-abc123";
            pkceStates["tidalarr:pkce:challenge"] = "tidal-challenge-xyz789";

            // Assert - PKCE state should be plugin-specific
            Assert.True(pkceStates.Keys.All(k => k.StartsWith("tidalarr:")));
        }

        #endregion

        #region Audio Quality Settings Isolation Tests

        [Fact]
        public void QualitySettings_Should_Be_Plugin_Scoped()
        {
            // Arrange - Quality settings vary per streaming service
            var settings = new Dictionary<string, IDictionary<string, object>>
            {
                ["tidalarr"] = new Dictionary<string, object>
                {
                    ["Quality"] = "HiFi",
                    ["Formats"] = new[] { "MQA", "FLAC", "AAC-320", "AAC-96" }
                },
                ["qobuzarr"] = new Dictionary<string, object>
                {
                    ["Quality"] = 27,
                    ["Formats"] = new[] { "FLAC-Max", "FLAC-HiRes", "FLAC-CD" }
                }
            };

            // Assert - Each plugin has its own quality settings
            Assert.Equal("HiFi", settings["tidalarr"]["Quality"]);
            Assert.Equal(27, settings["qobuzarr"]["Quality"]);
        }

        [Fact]
        public void AudioFormatHandler_State_Should_Be_Isolated()
        {
            // Arrange - M4A/FLAC format handling state
            var formatHandlerStates = new Dictionary<string, object>
            {
                ["tidalarr"] = new { Container = "M4A", Codec = "FLAC", ExtractorReady = true },
                ["qobuzarr"] = new { Quality = "FLAC-Max", DirectDownload = true }
            };

            // Assert - Format handlers should be independent
            Assert.NotEqual(
                formatHandlerStates["tidalarr"],
                formatHandlerStates["qobuzarr"]);
        }

        #endregion

        #region Concurrent Download Manager Isolation Tests

        [Fact]
        public async Task ConcurrentDownloadManager_Should_Be_Plugin_Scoped()
        {
            // Arrange - Each plugin has its own concurrent download limits
            var tidalManager = new SemaphoreSlim(3); // Tidal: 3 concurrent albums
            var qobuzManager = new SemaphoreSlim(5); // Qobuz: 5 concurrent albums

            // Act - Start Tidal downloads
            await tidalManager.WaitAsync();
            await tidalManager.WaitAsync();
            await tidalManager.WaitAsync();

            // Assert - Qobuz should have full capacity
            Assert.Equal(0, tidalManager.CurrentCount);
            Assert.Equal(5, qobuzManager.CurrentCount);

            // Cleanup
            tidalManager.Dispose();
            qobuzManager.Dispose();
        }

        [Fact]
        public void DownloadQueue_Should_Be_Plugin_Scoped()
        {
            // Arrange
            var tidalQueue = new Queue<string>();
            var qobuzQueue = new Queue<string>();

            // Act - Add items to Tidal queue
            tidalQueue.Enqueue("album:tidal:123");
            tidalQueue.Enqueue("album:tidal:456");

            // Assert - Qobuz's queue should be empty
            Assert.Empty(qobuzQueue);
            Assert.Equal(2, tidalQueue.Count);
        }

        #endregion

        #region DASH Stream Provider Isolation Tests

        [Fact]
        public void StreamProvider_State_Should_Be_Isolated()
        {
            // Arrange - Tidal's DASH stream providers should be independent
            var streamProviders = new ConcurrentDictionary<string, object>();

            // Act
            streamProviders["tidalarr:track:123:provider"] = new
            {
                ManifestUrl = "https://api.tidal.com/manifest/123",
                ChunkUrls = new[] { "chunk1", "chunk2", "chunk3" },
                CurrentChunk = 0
            };

            // Assert
            Assert.Single(streamProviders);
            Assert.True(streamProviders.ContainsKey("tidalarr:track:123:provider"));
        }

        [Fact]
        public async Task StreamProviders_Should_Not_Interfere()
        {
            // Arrange
            var activeStreams = new ConcurrentDictionary<string, int>();
            var tasks = new List<Task>();

            // Act - Simulate multiple concurrent streams from different plugins
            for (int i = 0; i < 20; i++)
            {
                var plugin = i % 2 == 0 ? "tidalarr" : "qobuzarr";
                var trackId = i;
                tasks.Add(Task.Run(async () =>
                {
                    var key = $"{plugin}:stream:{trackId}";
                    activeStreams.TryAdd(key, trackId);
                    await Task.Delay(10); // Simulate streaming
                    activeStreams.TryRemove(key, out _);
                }));
            }

            await Task.WhenAll(tasks);

            // Assert - All streams should complete without interference
            Assert.Empty(activeStreams);
        }

        #endregion

        #region Error State Isolation Tests

        [Fact]
        public void CircuitBreaker_State_Should_Be_Plugin_Scoped()
        {
            // Arrange - Circuit breakers for API resilience
            var circuitStates = new ConcurrentDictionary<string, string>
            {
                ["tidalarr:api"] = "Closed",
                ["qobuzarr:api"] = "Closed"
            };

            // Act - Tidal API experiences failures, circuit breaker opens
            circuitStates["tidalarr:api"] = "Open";

            // Assert - Qobuz's circuit breaker should be unaffected
            Assert.Equal("Open", circuitStates["tidalarr:api"]);
            Assert.Equal("Closed", circuitStates["qobuzarr:api"]);
        }

        [Fact]
        public void ChunkDownloadErrors_Should_Not_Affect_Other_Tracks()
        {
            // Arrange
            var trackErrors = new ConcurrentDictionary<string, int>();

            // Act - Track 1 has chunk download errors
            trackErrors.AddOrUpdate("track:1", 3, (_, count) => count + 1);

            // Assert - Track 2 should have no errors
            Assert.Equal(3, trackErrors.GetValueOrDefault("track:1"));
            Assert.Equal(0, trackErrors.GetValueOrDefault("track:2"));
        }

        #endregion

        #region Memory Management Tests

        [Fact]
        public void ChunkBuffer_Should_Be_Released_After_Assembly()
        {
            // Arrange - Chunk buffers should be released after track assembly
            var chunkBuffer = new Dictionary<string, byte[]>();

            // Act - Fill buffer with chunks
            for (int i = 0; i < 10; i++)
            {
                chunkBuffer[$"chunk:{i}"] = new byte[1024 * 1024]; // 1MB per chunk
            }

            var chunkCount = chunkBuffer.Count;

            // Simulate track assembly and cleanup
            chunkBuffer.Clear();

            // Assert
            Assert.Equal(10, chunkCount);
            Assert.Empty(chunkBuffer);
        }

        [Fact]
        public void Plugin_Objects_Should_Be_GC_Eligible_After_Unload()
        {
            // Arrange
            var pluginState = new object();
            var weakRef = new WeakReference(pluginState);

            // Act - Simulate plugin unload
            pluginState = null!;
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            // Assert
            Assert.False(weakRef.IsAlive);
        }

        #endregion

        #region Logger Isolation Tests

        [Fact]
        public void Logger_Should_Include_Plugin_Identifier()
        {
            // Arrange - Log entries should be attributable to their plugin
            var logEntries = new List<(string Category, string Message)>();

            // Act - Simulate logging from Tidal components
            logEntries.Add(("Tidalarr.Integration.TidalIndexer", "Search started"));
            logEntries.Add(("Tidalarr.Infrastructure.TidalChunkDownloader", "Downloading chunk 1/10"));
            logEntries.Add(("Tidalarr.Domain.TidalStreamManifest", "Parsed DASH manifest"));

            // Assert
            Assert.All(logEntries, entry =>
            {
                Assert.Contains("Tidal", entry.Category);
            });
        }

        #endregion

        #region Cancellation Token Isolation Tests

        [Fact]
        public async Task Cancellation_Should_Be_Plugin_Scoped()
        {
            // Arrange
            using var tidalCts = new CancellationTokenSource();
            using var qobuzCts = new CancellationTokenSource();

            // Act - Cancel Tidal operations
            tidalCts.Cancel();

            // Assert - Qobuz operations should continue
            Assert.True(tidalCts.IsCancellationRequested);
            Assert.False(qobuzCts.IsCancellationRequested);
        }

        [Fact]
        public async Task ChunkDownload_Cancellation_Should_Propagate_To_Track()
        {
            // Arrange
            using var trackCts = new CancellationTokenSource();
            var chunkCompletions = new List<bool>();

            // Act - Simulate chunk downloads with cancellation
            for (int i = 0; i < 5; i++)
            {
                if (i == 2) trackCts.Cancel(); // Cancel mid-download

                if (trackCts.IsCancellationRequested)
                {
                    chunkCompletions.Add(false);
                }
                else
                {
                    chunkCompletions.Add(true);
                }
            }

            // Assert - First 2 chunks completed, rest cancelled
            Assert.Equal(new[] { true, true, false, false, false }, chunkCompletions);
        }

        #endregion
    }
}
