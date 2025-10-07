using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Lidarr.Plugin.Common.Security;
using Lidarr.Plugin.Common.Services;
using Lidarr.Plugin.Common.Services.Intelligence;
using Lidarr.Plugin.Abstractions.Models;
using Lidarr.Plugin.Common.Utilities;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Quality;

namespace Tidalarr.Application.Services;

public class TidalSearchService
{
    private readonly ITidalCore _apiClient;
    private readonly TidalQualityDetector _qualityDetector;
    private readonly IQueryOptimizer? _queryOptimizer;
    
    public TidalSearchService(ITidalCore apiClient, TidalQualityDetector qualityDetector, IQueryOptimizer? queryOptimizer = null)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _qualityDetector = qualityDetector ?? throw new ArgumentNullException(nameof(qualityDetector));
        _queryOptimizer = queryOptimizer; // Optional for backward compatibility
    }
    
    public async Task<TidalSearchResults> SearchWithQualityDetectionAsync(string query, TidalQuality preferredQuality = TidalQuality.Lossless)
    {
        // Validate and normalize input (URL encoding handled by request builder later)
        Guard.NotNullOrWhiteSpace(query, nameof(query));
        var sanitizedQuery = Sanitize.DisplayText(query);
        
        // Optimize query if optimizer is available
        var optimizedQuery = sanitizedQuery;
        if (_queryOptimizer != null)
        {
            var context = new QueryContext
            {
                Type = QueryType.Album,
                PreferredQuality = MapToStreamingQualityTier(preferredQuality),
                Country = "US" // Could be made configurable
            };
            
            var optimization = await _queryOptimizer.OptimizeQueryAsync(sanitizedQuery, context);
            optimizedQuery = optimization.Query;
        }
        
        // Execute search with safe error handling
        var stopwatch = Stopwatch.StartNew();
        var (success, searchResults) = await SafeOperationExecutor.TryExecuteAsync<TidalSearchResults>(() => 
            _apiClient.SearchAsync(optimizedQuery));
            
        if (!success || searchResults == null)
        {
            return new TidalSearchResults(
                Albums: new List<TidalAlbumInfo>(),
                Tracks: new List<TidalTrackInfo>(),
                TotalCount: 0,
                HasMore: false
            );
        }
        
        stopwatch.Stop();
        
        // Learn from results if optimizer is available
        if (_queryOptimizer != null)
        {
            var queryResults = new QueryResults
            {
                ResultCount = searchResults.TotalCount,
                ExecutionTime = stopwatch.Elapsed,
                RelevanceScore = CalculateRelevanceScore(searchResults)
            };
            
            var feedback = new QueryFeedback
            {
                Satisfied = searchResults.TotalCount > 0,
                Rating = searchResults.TotalCount > 0 ? 4 : 2 // Simple scoring
            };
            
            // Fire-and-forget learning
            _ = Task.Run(() => _queryOptimizer.LearnFromResultsAsync(optimizedQuery, queryResults, feedback));
        }
        
        // Enhance results with quality detection
        var enhancedAlbums = searchResults.Albums.Select(album => 
            EnhanceAlbumWithQuality(album, preferredQuality)).ToList();
            
        var enhancedTracksAll = searchResults.Tracks.Select(track => 
            EnhanceTrackWithQuality(track, preferredQuality)).ToList();

        // Filter likely preview/sample content early
        var enhancedTracks = enhancedTracksAll
            .Where(t => !Lidarr.Plugin.Common.Utilities.PreviewDetectionUtility.IsLikelyPreview(
                url: string.Empty,
                durationSeconds: t.Duration,
                restrictionMessage: string.Empty))
            .ToList();
        
        return new TidalSearchResults(
            Albums: enhancedAlbums,
            Tracks: enhancedTracks,
            TotalCount: enhancedAlbums.Count + enhancedTracks.Count,
            HasMore: searchResults.HasMore
        );
    }
    
    public async Task<TidalSearchResults> SearchByTypeAsync(string query, TidalSearchType searchType, int limit = 100)
    {
        // Validate and normalize input (URL encoding handled by request builder later)
        Guard.NotNullOrWhiteSpace(query, nameof(query));
        var sanitizedQuery = Sanitize.DisplayText(query);
        
        // Optimize query based on search type
        var optimizedQuery = sanitizedQuery;
        if (_queryOptimizer != null)
        {
            var context = new QueryContext
            {
                Type = MapSearchTypeToQueryType(searchType),
                Country = "US"
            };
            
            var optimization = await _queryOptimizer.OptimizeQueryAsync(sanitizedQuery, context);
            optimizedQuery = optimization.Query;
        }
        
        // Execute search with error handling
        var (success, allResults) = await SafeOperationExecutor.TryExecuteAsync<TidalSearchResults>(() => 
            _apiClient.SearchAsync(optimizedQuery, limit));
            
        if (!success || allResults == null)
        {
            return new TidalSearchResults(
                Albums: new List<TidalAlbumInfo>(),
                Tracks: new List<TidalTrackInfo>(),
                TotalCount: 0,
                HasMore: false
            );
        }
        
        return searchType switch
        {
            TidalSearchType.Album => new TidalSearchResults(
                Albums: allResults.Albums,
                Tracks: new List<TidalTrackInfo>(),
                TotalCount: allResults.Albums.Count,
                HasMore: false
            ),
            TidalSearchType.Track => new TidalSearchResults(
                Albums: new List<TidalAlbumInfo>(),
                Tracks: allResults.Tracks,
                TotalCount: allResults.Tracks.Count,
                HasMore: false
            ),
            TidalSearchType.All => allResults,
            _ => allResults
        };
    }
    
    public async Task<TidalAlbumInfo> GetAlbumWithTracksAsync(string albumId)
    {
        Guard.NotNullOrWhiteSpace(albumId, nameof(albumId));
        
        var (success, album) = await SafeOperationExecutor.TryExecuteAsync<TidalAlbumInfo>(() => 
            _apiClient.GetAlbumAsync(albumId));
            
        if (!success || album == null)
        {
            throw new InvalidOperationException($"Failed to retrieve album with ID: {albumId}");
        }
        
        // TODO: Load album tracks - for now return basic album info
        return album;
    }
    
    private TidalAlbumInfo EnhanceAlbumWithQuality(TidalAlbumInfo album, TidalQuality preferredQuality)
    {
        // For now, assume all albums have the basic qualities
        // TODO: Enhance with actual quality detection from API
        var enhancedQualities = new List<TidalQuality> { TidalQuality.Low, TidalQuality.High, TidalQuality.Lossless };
        
        return album with { AvailableQualities = enhancedQualities };
    }
    
    private TidalTrackInfo EnhanceTrackWithQuality(TidalTrackInfo track, TidalQuality preferredQuality)
    {
        // Select best available quality for the track
        var availableQualities = new[] { TidalQuality.Low, TidalQuality.High, TidalQuality.Lossless };
        var bestQuality = _qualityDetector.SelectBestQuality(availableQualities, preferredQuality);
        
        return track with { Quality = bestQuality };
    }
    
    private static StreamingQualityTier MapToStreamingQualityTier(TidalQuality quality)
    {
        return quality switch
        {
            TidalQuality.Low => StreamingQualityTier.Low,
            TidalQuality.High => StreamingQualityTier.High,
            TidalQuality.Lossless => StreamingQualityTier.Lossless,
            TidalQuality.HiRes => StreamingQualityTier.HiRes,
            _ => StreamingQualityTier.High
        };
    }
    
    private static QueryType MapSearchTypeToQueryType(TidalSearchType searchType)
    {
        return searchType switch
        {
            TidalSearchType.Album => QueryType.Album,
            TidalSearchType.Track => QueryType.Track,
            TidalSearchType.Artist => QueryType.Artist,
            TidalSearchType.All => QueryType.Album,
            _ => QueryType.Album
        };
    }
    
    private static double CalculateRelevanceScore(TidalSearchResults results)
    {
        if (results.TotalCount == 0) return 0.0;
        
        // Simple relevance scoring based on result count and diversity
        var albumScore = results.Albums.Count * 0.6;
        var trackScore = results.Tracks.Count * 0.4;
        
        return Math.Min(1.0, (albumScore + trackScore) / 100.0);
    }
}

public enum TidalSearchType
{
    All,
    Album,
    Track,
    Artist
}

