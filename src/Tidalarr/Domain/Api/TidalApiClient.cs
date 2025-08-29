using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Lidarr.Plugin.Common.Interfaces;
using Lidarr.Plugin.Common.Services.Http;
using Lidarr.Plugin.Common.Services.Caching;
using Lidarr.Plugin.Common.Utilities;
using Tidalarr.Core.Constants;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;

namespace Tidalarr.Domain.Api;

public class TidalApiClient : ITidalCore, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly StreamingApiRequestBuilder _requestBuilder;
    private readonly IStreamingResponseCache _cache;
    private readonly ITidalAuth _authService;
    
    public TidalApiClient(HttpClient httpClient, ITidalAuth authService, IStreamingResponseCache cache = null)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _requestBuilder = new StreamingApiRequestBuilder(TidalConstants.API_V1_BASE);
        _cache = cache;
    }
    
    public async Task<TidalTrackInfo> GetTrackAsync(string trackId, CancellationToken cancellationToken = default)
    {
        var tokens = await _authService.GetValidTokensAsync();
        
        var parameters = new Dictionary<string, string>
        {
            ["sessionId"] = tokens.SessionId,
            ["countryCode"] = tokens.CountryCode,
            ["limit"] = TidalConstants.DEFAULT_ITEM_LIMIT.ToString()
        };
        
        var endpoint = $"tracks/{trackId}";
        
        // Check cache first if available
        if (_cache != null)
        {
            var cached = _cache.Get<TidalTrackDto>(endpoint, parameters);
            if (cached != null)
                return MapToTidalTrackInfo(cached);
        }
        
        var request = _requestBuilder
            .Endpoint(endpoint)
            .QueryParams(parameters)
            .BearerToken(tokens.AccessToken)
            .Build();
            
        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        
        var content = await response.Content.ReadAsStringAsync();
        var trackDto = JsonSerializer.Deserialize<TidalTrackDto>(content);
        
        if (trackDto == null)
            throw new InvalidOperationException("Failed to parse track response");
            
        // Cache the result if caching is available
        _cache?.Set(endpoint, parameters, trackDto, TimeSpan.FromMinutes(30));
            
        return MapToTidalTrackInfo(trackDto);
    }
    
    public async Task<TidalAlbumInfo> GetAlbumAsync(string albumId, CancellationToken cancellationToken = default)
    {
        var tokens = await _authService.GetValidTokensAsync();
        
        var parameters = new Dictionary<string, string>
        {
            ["sessionId"] = tokens.SessionId,
            ["countryCode"] = tokens.CountryCode,
            ["limit"] = TidalConstants.DEFAULT_ITEM_LIMIT.ToString()
        };
        
        var endpoint = $"albums/{albumId}";
        
        // Check cache first if available
        if (_cache != null)
        {
            var cached = _cache.Get<TidalAlbumDto>(endpoint, parameters);
            if (cached != null)
                return MapToTidalAlbumInfo(cached);
        }
        
        var request = _requestBuilder
            .Endpoint(endpoint)
            .QueryParams(parameters)
            .BearerToken(tokens.AccessToken)
            .Build();
            
        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        
        var content = await response.Content.ReadAsStringAsync();
        var albumDto = JsonSerializer.Deserialize<TidalAlbumDto>(content);
        
        if (albumDto == null)
            throw new InvalidOperationException("Failed to parse album response");
            
        // Cache albums for 2 hours
        _cache?.Set(endpoint, parameters, albumDto, TimeSpan.FromHours(2));
            
        return MapToTidalAlbumInfo(albumDto);
    }
    
    public async Task<TidalSearchResults> SearchAsync(string query, int limit = 100, CancellationToken cancellationToken = default)
    {
        var tokens = await _authService.GetValidTokensAsync();
        
        var parameters = new Dictionary<string, string>
        {
            ["query"] = query,
            ["types"] = "albums,tracks",
            ["limit"] = limit.ToString(),
            ["sessionId"] = tokens.SessionId,
            ["countryCode"] = tokens.CountryCode
        };
        
        var endpoint = "search";
        
        // Check cache first if available (searches cached for 5 minutes)
        if (_cache != null)
        {
            var cached = _cache.Get<TidalSearchResponseDto>(endpoint, parameters);
            if (cached != null)
                return MapToTidalSearchResults(cached);
        }
        
        var request = _requestBuilder
            .Endpoint(endpoint)
            .QueryParams(parameters)
            .BearerToken(tokens.AccessToken)
            .Build();
            
        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        
        var content = await response.Content.ReadAsStringAsync();
        var searchDto = JsonSerializer.Deserialize<TidalSearchResponseDto>(content);
        
        if (searchDto == null)
            throw new InvalidOperationException("Failed to parse search response");
            
        // Cache searches for 5 minutes
        _cache?.Set(endpoint, parameters, searchDto, TimeSpan.FromMinutes(5));
            
        return MapToTidalSearchResults(searchDto);
    }
    
    public async Task<List<TidalTrackInfo>> GetAlbumTracksAsync(string albumId, CancellationToken cancellationToken = default)
    {
        var tokens = await _authService.GetValidTokensAsync();
        
        var parameters = new Dictionary<string, string>
        {
            ["sessionId"] = tokens.SessionId,
            ["countryCode"] = tokens.CountryCode,
            ["limit"] = "100", // Get up to 100 tracks per album
            ["offset"] = "0"
        };
        
        var endpoint = $"albums/{albumId}/tracks";
        
        // Check cache first if available
        if (_cache != null)
        {
            var cached = _cache.Get<TidalAlbumTracksDto>(endpoint, parameters);
            if (cached?.items != null)
                return cached.items.Select(MapToTidalTrackInfo).ToList();
        }
        
        var request = _requestBuilder
            .Endpoint(endpoint)
            .QueryParams(parameters)
            .BearerToken(tokens.AccessToken)
            .Build();
            
        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        
        var content = await response.Content.ReadAsStringAsync();
        var tracksDto = JsonSerializer.Deserialize<TidalAlbumTracksDto>(content);
        
        if (tracksDto?.items == null)
            return new List<TidalTrackInfo>();
            
        // Cache album tracks for 2 hours
        _cache?.Set(endpoint, parameters, tracksDto, TimeSpan.FromHours(2));
            
        return tracksDto.items.Select(MapToTidalTrackInfo).ToList();
    }

    public async Task<TidalStreamInfo> GetStreamInfoAsync(string trackId, TidalQuality quality, CancellationToken cancellationToken = default)
    {
        var tokens = await _authService.GetValidTokensAsync();
        
        var qualityParam = TidalConstants.QualityParameters[quality];
        var parameters = new Dictionary<string, string>
        {
            ["audioquality"] = qualityParam,
            ["playbackmode"] = "STREAM",
            ["assetpresentation"] = "FULL",
            ["sessionId"] = tokens.SessionId,
            ["countryCode"] = tokens.CountryCode
        };
        
        var endpoint = $"tracks/{trackId}/playbackinfopostpaywall";
        
        // Don't cache playback info as it contains temporary URLs
        var request = _requestBuilder
            .Endpoint(endpoint)
            .QueryParams(parameters)
            .BearerToken(tokens.AccessToken)
            .Build();
            
        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        
        var content = await response.Content.ReadAsStringAsync();
        var playbackDto = JsonSerializer.Deserialize<TidalPlaybackInfoDto>(content);
        
        if (playbackDto == null)
            throw new InvalidOperationException("Failed to parse playback info response");
            
        return MapToTidalStreamInfo(trackId, playbackDto);
    }
    
    public async Task<bool> IsAuthenticatedAsync()
    {
        try
        {
            var tokens = await _authService.GetValidTokensAsync();
            return !string.IsNullOrEmpty(tokens?.AccessToken);
        }
        catch
        {
            return false;
        }
    }
    
    // Uses StreamingApiRequestBuilder for consistent HTTP request handling
    // Integrates with shared library caching when available
    
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
            Tracks: new List<TidalTrackInfo>(), // Will be loaded separately via GetAlbumTracksAsync
            AvailableQualities: DetectAlbumQualities(dto),
            ReleaseDate: ParseReleaseDate(dto.releaseDate),
            CoverArtId: dto.cover ?? string.Empty,
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
            ChunkUrls: new[] { "https://test.tidal.com/chunk1.flac" }, // Legacy - use StreamManifest for actual parsing
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
    
    private static List<TidalQuality> DetectAlbumQualities(TidalAlbumDto dto)
    {
        var qualities = new List<TidalQuality> { TidalQuality.Low, TidalQuality.High };
        
        // Add lossless if the album supports it (most Tidal albums do)
        qualities.Add(TidalQuality.Lossless);
        
        // Add hi-res if it's a Master album (MQA)
        if (dto.audioQuality?.Contains("HI_RES") == true || dto.audioModes?.Contains("STEREO") == true)
        {
            qualities.Add(TidalQuality.HiRes);
        }
        
        return qualities;
    }
    
    private static DateTime ParseReleaseDate(string releaseDate)
    {
        if (DateTime.TryParse(releaseDate, out var date))
            return date;
        return DateTime.MinValue;
    }
    
    /// <summary>
    /// Get album with tracks loaded
    /// </summary>
    public async Task<TidalAlbumInfo> GetAlbumWithTracksAsync(string albumId, CancellationToken cancellationToken = default)
    {
        var album = await GetAlbumAsync(albumId, cancellationToken);
        var tracks = await GetAlbumTracksAsync(albumId, cancellationToken);
        
        // Return new album instance with tracks populated
        return new TidalAlbumInfo(
            Id: album.Id,
            Title: album.Title,
            Artists: album.Artists,
            Tracks: tracks,
            AvailableQualities: album.AvailableQualities,
            ReleaseDate: album.ReleaseDate,
            CoverArtId: album.CoverArtId,
            IsAvailable: album.IsAvailable
        );
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}
