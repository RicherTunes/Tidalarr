using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Quality;

namespace Tidalarr.Application.Services;

public class TidalSearchService
{
    private readonly ITidalCore _apiClient;
    private readonly TidalQualityDetector _qualityDetector;
    
    public TidalSearchService(ITidalCore apiClient, TidalQualityDetector qualityDetector)
    {
        _apiClient = apiClient;
        _qualityDetector = qualityDetector;
    }
    
    public async Task<TidalSearchResults> SearchWithQualityDetectionAsync(string query, TidalQuality preferredQuality = TidalQuality.Lossless)
    {
        var searchResults = await _apiClient.SearchAsync(query);
        
        // Enhance results with quality detection
        var enhancedAlbums = searchResults.Albums.Select(album => 
            EnhanceAlbumWithQuality(album, preferredQuality)).ToList();
            
        var enhancedTracks = searchResults.Tracks.Select(track => 
            EnhanceTrackWithQuality(track, preferredQuality)).ToList();
        
        return new TidalSearchResults(
            Albums: enhancedAlbums,
            Tracks: enhancedTracks,
            TotalCount: enhancedAlbums.Count + enhancedTracks.Count,
            HasMore: searchResults.HasMore
        );
    }
    
    public async Task<TidalSearchResults> SearchByTypeAsync(string query, TidalSearchType searchType, int limit = 100)
    {
        var allResults = await _apiClient.SearchAsync(query, limit);
        
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
        var album = await _apiClient.GetAlbumAsync(albumId);
        
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
}

public enum TidalSearchType
{
    All,
    Album,
    Track,
    Artist
}
