using System.Collections.Generic;
using System.Text.Json;
using Lidarr.Plugin.Common.Services.Http;
using Lidarr.Plugin.Common.Utilities;
using Tidalarr.Core.Constants;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;

namespace Tidalarr.Domain.Api;

public class TidalApiClient : ITidalCore, IDisposable
{
    private readonly IEnhancedStreamingApiClient _apiClient;
    private readonly ITidalAuth _authService;
    
    public TidalApiClient(HttpClient httpClient, ITidalAuth authService)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _apiClient = new EnhancedStreamingApiClient(
            httpClient ?? throw new ArgumentNullException(nameof(httpClient)),
            "Tidal",
            TidalConstants.API_V1_BASE);
    }
    
    public async Task<TidalTrackInfo> GetTrackAsync(string trackId, CancellationToken cancellationToken = default)
    {
        var tokens = await _authService.GetValidTokensAsync();
        _apiClient.SetAuthenticationToken(tokens.AccessToken, AuthenticationType.Bearer);
        
        var parameters = new Dictionary<string, string>
        {
            ["sessionId"] = tokens.SessionId,
            ["countryCode"] = tokens.CountryCode,
            ["limit"] = TidalConstants.DEFAULT_ITEM_LIMIT.ToString()
        };
        
        var trackDto = await _apiClient.GetAsync<TidalTrackDto>(
            $"tracks/{trackId}", 
            parameters, 
            CachePolicy.Medium, // Cache tracks for 30 minutes
            cancellationToken);
        
        if (trackDto == null)
            throw new InvalidOperationException("Failed to parse track response");
            
        return MapToTidalTrackInfo(trackDto);
    }
    
    public async Task<TidalAlbumInfo> GetAlbumAsync(string albumId, CancellationToken cancellationToken = default)
    {
        var tokens = await _authService.GetValidTokensAsync();
        _apiClient.SetAuthenticationToken(tokens.AccessToken, AuthenticationType.Bearer);
        
        var parameters = new Dictionary<string, string>
        {
            ["sessionId"] = tokens.SessionId,
            ["countryCode"] = tokens.CountryCode,
            ["limit"] = TidalConstants.DEFAULT_ITEM_LIMIT.ToString()
        };
        
        var albumDto = await _apiClient.GetAsync<TidalAlbumDto>(
            $"albums/{albumId}", 
            parameters, 
            CachePolicy.Long, // Cache albums for 2 hours
            cancellationToken);
        
        if (albumDto == null)
            throw new InvalidOperationException("Failed to parse album response");
            
        return MapToTidalAlbumInfo(albumDto);
    }
    
    public async Task<TidalSearchResults> SearchAsync(string query, int limit = 100, CancellationToken cancellationToken = default)
    {
        var tokens = await _authService.GetValidTokensAsync();
        _apiClient.SetAuthenticationToken(tokens.AccessToken, AuthenticationType.Bearer);
        
        var parameters = new Dictionary<string, string>
        {
            ["query"] = query,
            ["types"] = "albums,tracks",
            ["limit"] = limit.ToString(),
            ["sessionId"] = tokens.SessionId,
            ["countryCode"] = tokens.CountryCode
        };
        
        var searchDto = await _apiClient.GetAsync<TidalSearchResponseDto>(
            "search", 
            parameters, 
            CachePolicy.Short, // Cache searches for 5 minutes
            cancellationToken);
        
        if (searchDto == null)
            throw new InvalidOperationException("Failed to parse search response");
            
        return MapToTidalSearchResults(searchDto);
    }
    
    public async Task<TidalStreamInfo> GetStreamInfoAsync(string trackId, TidalQuality quality, CancellationToken cancellationToken = default)
    {
        var tokens = await _authService.GetValidTokensAsync();
        _apiClient.SetAuthenticationToken(tokens.AccessToken, AuthenticationType.Bearer);
        
        var qualityParam = TidalConstants.QualityParameters[quality];
        var parameters = new Dictionary<string, string>
        {
            ["audioquality"] = qualityParam,
            ["playbackmode"] = "STREAM",
            ["assetpresentation"] = "FULL",
            ["sessionId"] = tokens.SessionId,
            ["countryCode"] = tokens.CountryCode
        };
        
        // Don't cache playback info as it contains temporary URLs
        var playbackDto = await _apiClient.GetAsync<TidalPlaybackInfoDto>(
            $"tracks/{trackId}/playbackinfopostpaywall", 
            parameters, 
            null, // No caching for stream URLs
            cancellationToken);
        
        if (playbackDto == null)
            throw new InvalidOperationException("Failed to parse playback info response");
            
        return MapToTidalStreamInfo(trackId, playbackDto);
    }
    
    // Enhanced API client handles authentication, retry logic, and caching automatically
    // No need for manual HTTP request building
    
    private static TidalTrackInfo MapToTidalTrackInfo(TidalTrackDto dto)
    {
        return new TidalTrackInfo(
            Id: dto.id,
            Title: dto.title,
            Artists: new List<string> { dto.artist.name },
            AlbumId: dto.album.id,
            AlbumTitle: dto.album.title,
            TrackNumber: dto.trackNumber,
            Duration: dto.duration,
            Quality: MapQualityFromString(dto.audioQuality),
            IsAvailable: dto.streamReady,
            ReleaseDate: DateTime.MinValue // TODO: Add releaseDate to DTO
        );
    }
    
    private static TidalAlbumInfo MapToTidalAlbumInfo(TidalAlbumDto dto)
    {
        return new TidalAlbumInfo(
            Id: dto.id,
            Title: dto.title,
            Artists: new List<string> { dto.artist.name },
            Tracks: new List<TidalTrackInfo>(), // TODO: Load tracks separately
            AvailableQualities: new List<TidalQuality> { TidalQuality.Lossless }, // TODO: Detect from API
            ReleaseDate: DateTime.MinValue, // TODO: Add releaseDate to DTO
            CoverArtId: string.Empty, // TODO: Add cover to DTO
            IsAvailable: true
        );
    }
    
    private static TidalSearchResults MapToTidalSearchResults(TidalSearchResponseDto dto)
    {
        return new TidalSearchResults(
            Albums: dto.albums.items.Select(MapToTidalAlbumInfo).ToList(),
            Tracks: dto.tracks.items.Select(MapToTidalTrackInfo).ToList(),
            TotalCount: dto.albums.items.Count + dto.tracks.items.Count,
            HasMore: false // TODO: Implement pagination detection
        );
    }
    
    private static TidalStreamInfo MapToTidalStreamInfo(string trackId, TidalPlaybackInfoDto dto)
    {
        // Basic implementation - manifest parsing will be enhanced later
        return new TidalStreamInfo(
            TrackId: trackId,
            ChunkUrls: new[] { "https://test.tidal.com/chunk1.flac" }, // TODO: Parse manifest
            FileExtension: ".flac",
            MimeType: dto.manifestMimeType,
            IsEncrypted: dto.encryptionType != "NONE",
            SecurityToken: null
        );
    }
    
    private static TidalQuality MapQualityFromString(string quality)
    {
        return quality switch
        {
            "LOW" => TidalQuality.Low,
            "HIGH" => TidalQuality.High,
            "LOSSLESS" => TidalQuality.Lossless,
            "HI_RES_LOSSLESS" => TidalQuality.HiRes,
            _ => TidalQuality.High
        };
    }
    
    public void Dispose()
    {
        _apiClient?.Dispose();
    }
}
