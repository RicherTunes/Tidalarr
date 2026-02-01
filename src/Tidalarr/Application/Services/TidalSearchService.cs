using System.Diagnostics;
using Lidarr.Plugin.Common.Security;
using Lidarr.Plugin.Common.Services;
using Lidarr.Plugin.Common.Services.Intelligence;
using Lidarr.Plugin.Abstractions.Models;
using Lidarr.Plugin.Common.Utilities;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Quality;

namespace Tidalarr.Application.Services;

public class TidalSearchService(ITidalCore apiClient, TidalQualityDetector qualityDetector, IQueryOptimizer? queryOptimizer = null)
{
    private readonly ITidalCore _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
    private readonly TidalQualityDetector _qualityDetector = qualityDetector ?? throw new ArgumentNullException(nameof(qualityDetector));
    private readonly IQueryOptimizer? _queryOptimizer = queryOptimizer;

    public async Task<TidalSearchResults> SearchWithQualityDetectionAsync(string query, TidalQuality preferredQuality = TidalQuality.Lossless)
    {
        // Validate and normalize input (URL encoding handled by request builder later)
        _ = Guard.NotNullOrWhiteSpace(query, nameof(query));
        string sanitizedQuery = Sanitize.DisplayText(query);

        // Optimize query if optimizer is available
        string optimizedQuery = sanitizedQuery;
        if (this._queryOptimizer != null)
        {
            QueryContext context = new()
            {
                Type = QueryType.Album,
                PreferredQuality = MapToStreamingQualityTier(preferredQuality),
                Country = "US" // Could be made configurable
            };

            OptimizedQuery optimization = await this._queryOptimizer.OptimizeQueryAsync(sanitizedQuery, context);
            optimizedQuery = optimization.Query;
        }

        // Execute search
        Stopwatch stopwatch = Stopwatch.StartNew();
        TidalSearchResults searchResults = await this._apiClient.SearchAsync(optimizedQuery);

        stopwatch.Stop();

        // Learn from results if optimizer is available
        if (this._queryOptimizer != null)
        {
            QueryResults queryResults = new()
            {
                ResultCount = searchResults.TotalCount,
                ExecutionTime = stopwatch.Elapsed,
                RelevanceScore = CalculateRelevanceScore(searchResults)
            };

            QueryFeedback feedback = new()
            {
                Satisfied = searchResults.TotalCount > 0,
                Rating = searchResults.TotalCount > 0 ? 4 : 2 // Simple scoring
            };

            // Fire-and-forget learning
            _ = Task.Run(() => this._queryOptimizer.LearnFromResultsAsync(optimizedQuery, queryResults, feedback));
        }

        // Enhance results with quality detection
        List<TidalAlbumInfo> enhancedAlbums = [.. searchResults.Albums.Select(album =>
            EnhanceAlbumWithQuality(album, preferredQuality))];

        List<TidalTrackInfo> enhancedTracksAll = [.. searchResults.Tracks.Select(track =>
            EnhanceTrackWithQuality(track, preferredQuality))];

        // Filter likely preview/sample content early
        List<TidalTrackInfo> enhancedTracks = [.. enhancedTracksAll
            .Where(t => !PreviewDetectionUtility.IsLikelyPreview(
                url: string.Empty,
                durationSeconds: t.Duration,
                restrictionMessage: string.Empty))];

        return new TidalSearchResults(
            Albums: enhancedAlbums,
            Tracks: enhancedTracks,
            Artists: searchResults.Artists,
            TotalCount: enhancedAlbums.Count + enhancedTracks.Count + searchResults.Artists.Count,
            HasMore: searchResults.HasMore
        );
    }

    public async Task<TidalSearchResults> SearchByTypeAsync(string query, TidalSearchType searchType, int limit = 100)
    {
        // Validate and normalize input (URL encoding handled by request builder later)
        _ = Guard.NotNullOrWhiteSpace(query, nameof(query));
        string sanitizedQuery = Sanitize.DisplayText(query);

        // Optimize query based on search type
        string optimizedQuery = sanitizedQuery;
        if (this._queryOptimizer != null)
        {
            QueryContext context = new()
            {
                Type = MapSearchTypeToQueryType(searchType),
                Country = "US"
            };

            OptimizedQuery optimization = await this._queryOptimizer.OptimizeQueryAsync(sanitizedQuery, context);
            optimizedQuery = optimization.Query;
        }

        // Execute search with error handling
        (bool success, TidalSearchResults allResults) = await SafeOperationExecutor.TryExecuteAsync<TidalSearchResults>(() =>
            this._apiClient.SearchAsync(optimizedQuery, limit));

        return !success || allResults == null
            ? new TidalSearchResults(
                Albums: [],
                Tracks: [],
                Artists: [],
                TotalCount: 0,
                HasMore: false
            )
            : searchType switch
            {
                TidalSearchType.Album => new TidalSearchResults(
                    Albums: allResults.Albums,
                    Tracks: [],
                    Artists: [],
                    TotalCount: allResults.Albums.Count,
                    HasMore: false
                ),
                TidalSearchType.Track => new TidalSearchResults(
                    Albums: [],
                    Tracks: allResults.Tracks,
                    Artists: [],
                    TotalCount: allResults.Tracks.Count,
                    HasMore: false
                ),
                TidalSearchType.Artist => new TidalSearchResults(
                    Albums: [],
                    Tracks: [],
                    Artists: allResults.Artists,
                    TotalCount: allResults.Artists.Count,
                    HasMore: false
                ),
                TidalSearchType.All => allResults,
                _ => allResults
            };
    }

    public async Task<TidalAlbumInfo> GetAlbumWithTracksAsync(string albumId)
    {
        _ = Guard.NotNullOrWhiteSpace(albumId, nameof(albumId));

        (bool success, TidalAlbumInfo album) = await SafeOperationExecutor.TryExecuteAsync<TidalAlbumInfo>(() =>
            this._apiClient.GetAlbumAsync(albumId));

        if (!success || album == null)
        {
            throw new InvalidOperationException($"Failed to retrieve album with ID: {albumId}");
        }

        // TODO: Load album tracks - for now return basic album info
        return album;
    }

    private TidalAlbumInfo EnhanceAlbumWithQuality(TidalAlbumInfo album, TidalQuality preferredQuality)
    {
        // Preserve API-detected qualities from audioQuality field
        // TidalApiClient.DetectAlbumQualities() already parses the audioQuality string
        // Only use fallback if no qualities were detected
        if (album.AvailableQualities != null && album.AvailableQualities.Count > 0)
        {
            return album; // Quality already detected from API response
        }

        // Fallback: assume basic qualities if detection failed
        List<TidalQuality> fallbackQualities = [TidalQuality.Low, TidalQuality.High, TidalQuality.Lossless];
        return album with { AvailableQualities = fallbackQualities };
    }

    private TidalTrackInfo EnhanceTrackWithQuality(TidalTrackInfo track, TidalQuality preferredQuality)
    {
        // Preserve API-detected quality from audioQuality field
        // TidalApiClient.MapToTidalTrackInfo() already parses audioQuality
        // Only override if the detected quality is invalid (High = 1, which is the default fallback)
        if (track.Quality != TidalQuality.High || track.Quality == preferredQuality)
        {
            return track; // Quality already detected from API response
        }

        // Fallback: use detector to select best quality from assumed availability
        TidalQuality[] assumedQualities = [TidalQuality.Low, TidalQuality.High, TidalQuality.Lossless];
        TidalQuality bestQuality = this._qualityDetector.SelectBestQuality(assumedQualities, preferredQuality);

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
        if (results.TotalCount == 0)
        {
            return 0.0;
        }

        // Simple relevance scoring based on result count and diversity
        double albumScore = results.Albums.Count * 0.6;
        double trackScore = results.Tracks.Count * 0.4;

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

