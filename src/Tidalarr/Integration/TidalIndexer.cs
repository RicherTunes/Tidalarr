using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentValidation.Results;
using Microsoft.Extensions.Logging;
using Lidarr.Plugin.Common.Base;
using Lidarr.Plugin.Common.Models;
using Tidalarr.Application.Services;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Core.Mappers;
using Tidalarr.Domain.Api;
using Tidalarr.Domain.Authentication;
using Tidalarr.Domain.Quality;
using Tidalarr.Infrastructure.Storage;

namespace Tidalarr.Integration;

public class TidalIndexer : BaseStreamingIndexer<TidalIndexerSettings>
{
    private readonly TidalSearchService _searchService;
    private readonly ITidalCore _apiClient;
    private readonly TidalModelMapper _mapper;
    
    protected override string ServiceName => "Tidal";
    protected override string ProtocolName => "tidal";
    
    public TidalIndexer(
        TidalSearchService searchService, 
        ITidalCore apiClient,
        TidalIndexerSettings settings,
        ILogger logger = null)
        : base(settings, logger)
    {
        _searchService = searchService;
        _apiClient = apiClient;
        _mapper = new TidalModelMapper();
    }
    
    // Implement required abstract methods from BaseStreamingIndexer
    protected override async Task<bool> AuthenticateAsync()
    {
        try
        {
            return await _apiClient.IsAuthenticatedAsync();
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Tidal authentication failed");
            return false;
        }
    }
    
    protected override async Task<List<StreamingAlbum>> SearchAlbumsAsync(string searchTerm)
    {
        try
        {
            var preferredQuality = TidalQuality.Lossless; // Use default for search
            var searchResults = await _searchService.SearchWithQualityDetectionAsync(searchTerm, preferredQuality);
            
            return searchResults.Albums?
                .Select(_mapper.ToStreamingAlbum)
                .Where(album => album != null)
                .ToList() ?? new List<StreamingAlbum>();
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, $"Failed to search albums for: {searchTerm}");
            return new List<StreamingAlbum>();
        }
    }
    
    protected override async Task<List<StreamingTrack>> SearchTracksAsync(string searchTerm)
    {
        try
        {
            var preferredQuality = TidalQuality.Lossless; // Use default for search
            var searchResults = await _searchService.SearchWithQualityDetectionAsync(searchTerm, preferredQuality);
            
            return searchResults.Tracks?
                .Select(_mapper.ToStreamingTrack)
                .Where(track => track != null)
                .ToList() ?? new List<StreamingTrack>();
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, $"Failed to search tracks for: {searchTerm}");
            return new List<StreamingTrack>();
        }
    }
    
    protected override async Task<StreamingAlbum> GetAlbumDetailsAsync(string albumId)
    {
        try
        {
            var tidalAlbum = await _apiClient.GetAlbumAsync(albumId);
            return _mapper.ToStreamingAlbum(tidalAlbum);
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, $"Failed to get album details for: {albumId}");
            return null;
        }
    }
    
    protected override ValidationResult ValidateSettings(TidalIndexerSettings settings)
    {
        var result = new ValidationResult();
        
        if (string.IsNullOrEmpty(settings.TidalMarket))
        {
            result.Errors.Add(new FluentValidation.Results.ValidationFailure("TidalMarket", "Tidal market is required"));
        }
        
        if (string.IsNullOrEmpty(settings.ConfigPath))
        {
            result.Errors.Add(new FluentValidation.Results.ValidationFailure("ConfigPath", "Config path is required"));
        }
        
        return result;
    }
    
    // Public API methods for backward compatibility and additional functionality
    public new async Task<List<TidalReleaseInfo>> SearchAsync(string query)
    {
        try
        {
            var preferredQuality = TidalQuality.Lossless; // Use default for search
            var searchResults = await _searchService.SearchWithQualityDetectionAsync(query, preferredQuality);
            
            return MapToReleaseInfo(searchResults);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Not authenticated"))
        {
            throw new InvalidOperationException("Tidal authentication required. Please complete OAuth flow first.", ex);
        }
    }
    
    /// <summary>
    /// Enhanced search with streaming models
    /// </summary>
    public async Task<List<StreamingSearchResult>> SearchEnhancedAsync(string query)
    {
        try
        {
            var preferredQuality = TidalQuality.Lossless; // Use default for search
            var searchResults = await _searchService.SearchWithQualityDetectionAsync(query, preferredQuality);
            
            return _mapper.ToStreamingSearchResults(searchResults);
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, $"Enhanced search failed for: {query}");
            return new List<StreamingSearchResult>();
        }
    }
    
    private List<TidalReleaseInfo> MapToReleaseInfo(TidalSearchResults searchResults)
    {
        var releases = new List<TidalReleaseInfo>();
        
        // Add albums
        if (searchResults?.Albums != null)
        {
            foreach (var album in searchResults.Albums)
            {
                releases.Add(new TidalReleaseInfo
                {
                    Id = album.Id,
                    Title = album.Title,
                    Artist = string.Join(", ", album.Artists ?? new List<string>()),
                    Type = "Album",
                    Quality = GetHighestQuality(album.AvailableQualities),
                    DownloadUrl = $"tidal://album/{album.Id}",
                    PublishDate = album.ReleaseDate,
                    TrackCount = album.Tracks?.Count ?? 0
                });
            }
        }
        
        // Add individual tracks as singles
        if (searchResults?.Tracks != null)
        {
            foreach (var track in searchResults.Tracks)
            {
                releases.Add(new TidalReleaseInfo
                {
                    Id = track.Id,
                    Title = track.Title,
                    Artist = string.Join(", ", track.Artists ?? new List<string>()),
                    Type = "Track",
                    Quality = track.Quality.ToString(),
                    DownloadUrl = $"tidal://track/{track.Id}",
                    PublishDate = track.ReleaseDate,
                    TrackCount = 1
                });
            }
        }
        
        return releases;
    }
    
    private static string GetHighestQuality(List<TidalQuality> qualities)
    {
        if (qualities == null || !qualities.Any()) return "High";
        
        var highest = qualities.Max();
        return highest.ToString();
    }
    
    private static TidalQuality ParsePreferredQuality(string? qualityString)
    {
        return qualityString?.ToLowerInvariant() switch
        {
            "low" => TidalQuality.Low,
            "high" => TidalQuality.High,
            "lossless" => TidalQuality.Lossless,
            "hires" => TidalQuality.HiRes,
            _ => TidalQuality.Lossless
        };
    }
}

/// <summary>
/// Legacy release info for backward compatibility
/// </summary>
public class TidalReleaseInfo
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Quality { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public DateTime PublishDate { get; set; }
    public int TrackCount { get; set; }
}