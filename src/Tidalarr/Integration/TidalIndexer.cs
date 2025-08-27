using Tidalarr.Application.Services;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Api;
using Tidalarr.Domain.Authentication;
using Tidalarr.Domain.Quality;
using Tidalarr.Infrastructure.Storage;

namespace Tidalarr.Integration;

public class TidalIndexer
{
    private readonly TidalSearchService _searchService;
    private readonly TidalSettings _settings;
    
    public TidalIndexer(TidalSearchService searchService, TidalSettings settings)
    {
        _searchService = searchService;
        _settings = settings;
    }
    
    public async Task<List<TidalReleaseInfo>> SearchAsync(string query)
    {
        try
        {
            var preferredQuality = ParsePreferredQuality(_settings.PreferredQuality);
            var searchResults = await _searchService.SearchWithQualityDetectionAsync(query, preferredQuality);
            
            return MapToReleaseInfo(searchResults);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Not authenticated"))
        {
            throw new InvalidOperationException("Tidal authentication required. Please complete OAuth flow first.", ex);
        }
    }
    
    private List<TidalReleaseInfo> MapToReleaseInfo(TidalSearchResults searchResults)
    {
        var releases = new List<TidalReleaseInfo>();
        
        // Add albums
        foreach (var album in searchResults.Albums)
        {
            releases.Add(new TidalReleaseInfo
            {
                Id = album.Id,
                Title = album.Title,
                Artist = string.Join(", ", album.Artists),
                Type = "Album",
                Quality = GetHighestQuality(album.AvailableQualities),
                DownloadUrl = $"tidal://album/{album.Id}",
                PublishDate = album.ReleaseDate,
                TrackCount = album.Tracks.Count
            });
        }
        
        // Add individual tracks as singles
        foreach (var track in searchResults.Tracks)
        {
            releases.Add(new TidalReleaseInfo
            {
                Id = track.Id,
                Title = track.Title,
                Artist = string.Join(", ", track.Artists),
                Type = "Track",
                Quality = track.Quality.ToString(),
                DownloadUrl = $"tidal://track/{track.Id}",
                PublishDate = track.ReleaseDate,
                TrackCount = 1
            });
        }
        
        return releases;
    }
    
    private static string GetHighestQuality(List<TidalQuality> qualities)
    {
        if (!qualities.Any()) return "High";
        
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
