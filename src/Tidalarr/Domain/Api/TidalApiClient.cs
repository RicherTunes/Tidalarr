using System.Text.Json;
using Lidarr.Plugin.Common.Services.Http;
using Lidarr.Plugin.Common.Utilities;
using Polly;
using Tidalarr.Core.Constants;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Infrastructure.Resilience;

namespace Tidalarr.Domain.Api;

public class TidalApiClient : ITidalCore
{
    private readonly HttpClient _httpClient;
    private readonly ITidalAuth _authService;
    private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy;
    
    public TidalApiClient(HttpClient httpClient, ITidalAuth authService)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _retryPolicy = TidalResiliencePolicy.CreateHttpRetryPolicy();
    }
    
    public async Task<TidalTrackInfo> GetTrackAsync(string trackId, CancellationToken cancellationToken = default)
    {
        var tokens = await _authService.GetValidTokensAsync();
        var request = BuildAuthenticatedRequest($"tracks/{trackId}", tokens);
        
        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var trackDto = JsonSerializer.Deserialize<TidalTrackDto>(content);
        
        if (trackDto == null)
            throw new InvalidOperationException("Failed to parse track response");
            
        return MapToTidalTrackInfo(trackDto);
    }
    
    public async Task<TidalAlbumInfo> GetAlbumAsync(string albumId, CancellationToken cancellationToken = default)
    {
        var tokens = await _authService.GetValidTokensAsync();
        var request = BuildAuthenticatedRequest($"albums/{albumId}", tokens);
        
        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var albumDto = JsonSerializer.Deserialize<TidalAlbumDto>(content);
        
        if (albumDto == null)
            throw new InvalidOperationException("Failed to parse album response");
            
        return MapToTidalAlbumInfo(albumDto);
    }
    
    public async Task<TidalSearchResults> SearchAsync(string query, int limit = 100, CancellationToken cancellationToken = default)
    {
        var tokens = await _authService.GetValidTokensAsync();
        // Use shared library HTTP builder (architect fix)
        var request = new StreamingApiRequestBuilder(TidalConstants.API_V1_BASE)
            .Endpoint("search")
            .Query("query", query)
            .Query("types", "albums,tracks")
            .Query("limit", limit.ToString())
            .Query("sessionId", tokens.SessionId)
            .Query("countryCode", tokens.CountryCode)
            .BearerToken(tokens.AccessToken)
            .WithStreamingDefaults("Tidalarr/1.0")
            .Build();
        
        // Use shared retry logic (architect fix)
        var response = await _httpClient.ExecuteWithRetryAsync(request, maxRetries: 3);
        var content = await response.Content.ReadContentSafelyAsync();
        var searchDto = JsonSerializer.Deserialize<TidalSearchResponseDto>(content);
        
        if (searchDto == null)
            throw new InvalidOperationException("Failed to parse search response");
            
        return MapToTidalSearchResults(searchDto);
    }
    
    public async Task<TidalStreamInfo> GetStreamInfoAsync(string trackId, TidalQuality quality, CancellationToken cancellationToken = default)
    {
        var tokens = await _authService.GetValidTokensAsync();
        var qualityParam = TidalConstants.QualityParameters[quality];
        var request = BuildAuthenticatedRequest($"tracks/{trackId}/playbackinfopostpaywall?audioquality={qualityParam}&playbackmode=STREAM&assetpresentation=FULL", tokens);
        
        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var playbackDto = JsonSerializer.Deserialize<TidalPlaybackInfoDto>(content);
        
        if (playbackDto == null)
            throw new InvalidOperationException("Failed to parse playback info response");
            
        return MapToTidalStreamInfo(trackId, playbackDto);
    }
    
    private HttpRequestMessage BuildAuthenticatedRequest(string endpoint, TidalTokens tokens)
    {
        var url = $"{TidalConstants.API_V1_BASE}{endpoint}";
        
        // Add session parameters to URL
        var separator = url.Contains('?') ? "&" : "?";
        url += $"{separator}sessionId={tokens.SessionId}&countryCode={tokens.CountryCode}&limit={TidalConstants.DEFAULT_ITEM_LIMIT}";
        
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(tokens.TokenType, tokens.AccessToken);
        
        return request;
    }
    
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
}
