using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Lidarr.Plugin.Common.Interfaces;
using Lidarr.Plugin.Common.Services.Caching;
using Lidarr.Plugin.Common.Services.Http;
using Tidalarr.Core.Constants;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;

namespace Tidalarr.Domain.Api;

public class TidalApiClient : ITidalCore, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly StreamingApiRequestBuilder _requestBuilder;
    private readonly IStreamingResponseCache? _cache;
    private readonly ITidalAuth _authService;
    private readonly Tidalarr.Domain.Streaming.TidalManifestParser? _manifestParser;

    public TidalApiClient(HttpClient httpClient, ITidalAuth authService, IStreamingResponseCache? cache = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _requestBuilder = new StreamingApiRequestBuilder(TidalConstants.API_V1_BASE);
        _cache = cache;
    }

    // Overload that allows DI to provide a manifest parser without breaking existing callers
    [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
    public TidalApiClient(HttpClient httpClient, ITidalAuth authService, Tidalarr.Domain.Streaming.TidalManifestParser manifestParser, IStreamingResponseCache? cache = null)
        : this(httpClient, authService, cache)
    {
        _manifestParser = manifestParser;
    }

    public async Task<TidalTrackInfo> GetTrackAsync(string trackId, CancellationToken cancellationToken = default)
    {
        var tokens = await _authService.GetValidTokensAsync();
        var endpoint = $"tracks/{trackId}";
        var parameters = new Dictionary<string, string>
        {
            ["sessionId"] = tokens.SessionId,
            ["countryCode"] = tokens.CountryCode
        };

        if (_cache?.Get<TidalTrackDto>(endpoint, parameters) is { } cachedTrack)
            return MapToTidalTrackInfo(cachedTrack);

        var request = _requestBuilder
            .Endpoint(endpoint)
            .QueryParams(parameters)
            .BearerToken(tokens.AccessToken)
            .WithStreamingDefaults("Tidalarr/1.0.0")
            .Build();

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var dto = JsonSerializer.Deserialize<TidalTrackDto>(content);
        if (dto == null) throw new InvalidOperationException("Failed to parse track response");
        _cache?.Set(endpoint, parameters, dto, TimeSpan.FromHours(1));
        return MapToTidalTrackInfo(dto);
    }

    public async Task<TidalAlbumInfo> GetAlbumAsync(string albumId, CancellationToken cancellationToken = default)
    {
        var tokens = await _authService.GetValidTokensAsync();
        var endpoint = $"albums/{albumId}";
        var parameters = new Dictionary<string, string>
        {
            ["sessionId"] = tokens.SessionId,
            ["countryCode"] = tokens.CountryCode
        };

        if (_cache?.Get<TidalAlbumDto>(endpoint, parameters) is { } cachedAlbum)
            return MapToTidalAlbumInfo(cachedAlbum);

        var request = _requestBuilder
            .Endpoint(endpoint)
            .QueryParams(parameters)
            .BearerToken(tokens.AccessToken)
            .WithStreamingDefaults("Tidalarr/1.0.0")
            .Build();

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var dto = JsonSerializer.Deserialize<TidalAlbumDto>(content);
        if (dto == null) throw new InvalidOperationException("Failed to parse album response");
        _cache?.Set(endpoint, parameters, dto, TimeSpan.FromHours(2));
        return MapToTidalAlbumInfo(dto);
    }

    public async Task<List<TidalTrackInfo>> GetAlbumTracksAsync(string albumId, CancellationToken cancellationToken = default)
    {
        var tokens = await _authService.GetValidTokensAsync();
        var endpoint = $"albums/{albumId}/tracks";
        var parameters = new Dictionary<string, string>
        {
            ["sessionId"] = tokens.SessionId,
            ["countryCode"] = tokens.CountryCode,
            ["limit"] = TidalConstants.DEFAULT_ITEM_LIMIT.ToString()
        };

        if (_cache?.Get<TidalAlbumTracksDto>(endpoint, parameters) is { } cached)
            return cached.items.Select(MapToTidalTrackInfo).ToList();

        var request = _requestBuilder
            .Endpoint(endpoint)
            .QueryParams(parameters)
            .BearerToken(tokens.AccessToken)
            .WithStreamingDefaults("Tidalarr/1.0.0")
            .Build();

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var dto = JsonSerializer.Deserialize<TidalAlbumTracksDto>(content);
        if (dto == null) throw new InvalidOperationException("Failed to parse album tracks response");
        _cache?.Set(endpoint, parameters, dto, TimeSpan.FromHours(2));
        return dto.items.Select(MapToTidalTrackInfo).ToList();
    }

    public async Task<TidalAlbumInfo> GetAlbumWithTracksAsync(string albumId, CancellationToken cancellationToken = default)
    {
        var album = await GetAlbumAsync(albumId, cancellationToken);
        var tracks = await GetAlbumTracksAsync(albumId, cancellationToken);
        return new TidalAlbumInfo(
            Id: album.Id,
            Title: album.Title,
            Artists: album.Artists,
            Tracks: tracks,
            AvailableQualities: album.AvailableQualities,
            ReleaseDate: album.ReleaseDate,
            CoverArtId: album.CoverArtId,
            IsAvailable: album.IsAvailable);
    }

    public async Task<TidalSearchResults> SearchAsync(string query, int limit = 100, CancellationToken cancellationToken = default)
    {
        var tokens = await _authService.GetValidTokensAsync();
        var endpoint = "search";
        var parameters = new Dictionary<string, string>
        {
            ["query"] = query,
            ["types"] = "albums,tracks",
            ["limit"] = limit.ToString(),
            ["sessionId"] = tokens.SessionId,
            ["countryCode"] = tokens.CountryCode
        };

        if (_cache?.Get<TidalSearchResponseDto>(endpoint, parameters) is { } cached)
            return MapToTidalSearchResults(cached);

        var request = _requestBuilder
            .Endpoint(endpoint)
            .QueryParams(parameters)
            .BearerToken(tokens.AccessToken)
            .WithStreamingDefaults("Tidalarr/1.0.0")
            .Build();

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var dto = JsonSerializer.Deserialize<TidalSearchResponseDto>(content);
        if (dto == null) throw new InvalidOperationException("Failed to parse search response");
        _cache?.Set(endpoint, parameters, dto, TimeSpan.FromMinutes(5));
        return MapToTidalSearchResults(dto);
    }

    public async Task<TidalStreamInfo> GetStreamInfoAsync(string trackId, TidalQuality quality, CancellationToken cancellationToken = default)
    {
        var tokens = await _authService.GetValidTokensAsync();
        var endpoint = $"tracks/{trackId}/playbackinfopostpaywall";
        var parameters = new Dictionary<string, string>
        {
            ["audioquality"] = TidalConstants.QualityParameters[quality],
            ["playbackmode"] = "STREAM",
            ["assetpresentation"] = "FULL",
            ["sessionId"] = tokens.SessionId,
            ["countryCode"] = tokens.CountryCode
        };

        var request = _requestBuilder
            .Endpoint(endpoint)
            .QueryParams(parameters)
            .BearerToken(tokens.AccessToken)
            .WithStreamingDefaults("Tidalarr/1.0.0")
            .Build();

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var dto = JsonSerializer.Deserialize<TidalPlaybackInfoDto>(content);
        if (dto == null) throw new InvalidOperationException("Failed to parse playback info");

        // If a manifest parser is available, parse chunk URLs and better infer extension
        if (_manifestParser != null && !string.IsNullOrEmpty(dto.manifest) && !string.IsNullOrEmpty(dto.manifestMimeType))
        {
            try
            {
                var parsed = _manifestParser.ParseManifest(dto.manifest, dto.manifestMimeType);
                return new TidalStreamInfo(
                    TrackId: trackId,
                    ChunkUrls: parsed.ChunkUrls,
                    FileExtension: parsed.FileExtension,
                    MimeType: parsed.MimeType,
                    IsEncrypted: parsed.IsEncrypted,
                    SecurityToken: dto.securityToken);
            }
            catch
            {
                // Fallback to legacy behavior below
            }
        }

        return new TidalStreamInfo(
            TrackId: trackId,
            ChunkUrls: Array.Empty<string>(),
            FileExtension: dto.manifestMimeType?.Contains("mp4", StringComparison.OrdinalIgnoreCase) == true ? ".m4a" : ".flac",
            MimeType: dto.manifestMimeType ?? string.Empty,
            IsEncrypted: !string.Equals(dto.encryptionType, "NONE", StringComparison.OrdinalIgnoreCase),
            SecurityToken: dto.securityToken);
    }

    // Raw playback-info fetch used by stream service for manifest parsing
    public async Task<TidalPlaybackInfoDto> GetPlaybackInfoAsync(string trackId, TidalQuality quality, CancellationToken cancellationToken = default)
    {
        var tokens = await _authService.GetValidTokensAsync();
        var endpoint = $"tracks/{trackId}/playbackinfopostpaywall";
        var parameters = new Dictionary<string, string>
        {
            ["audioquality"] = TidalConstants.QualityParameters[quality],
            ["playbackmode"] = "STREAM",
            ["assetpresentation"] = "FULL",
            ["sessionId"] = tokens.SessionId,
            ["countryCode"] = tokens.CountryCode
        };

        var request = _requestBuilder
            .Endpoint(endpoint)
            .QueryParams(parameters)
            .BearerToken(tokens.AccessToken)
            .WithStreamingDefaults("Tidalarr/1.0.0")
            .Build();

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var dto = JsonSerializer.Deserialize<TidalPlaybackInfoDto>(content);
        if (dto == null) throw new InvalidOperationException("Failed to parse playback info");
        return dto;
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
            ReleaseDate: DateTime.MinValue);
    }

    private static TidalAlbumInfo MapToTidalAlbumInfo(TidalAlbumDto dto)
    {
        return new TidalAlbumInfo(
            Id: dto.id,
            Title: dto.title,
            Artists: new List<string> { dto.artist.name },
            Tracks: new List<TidalTrackInfo>(),
            AvailableQualities: DetectAlbumQualities(dto),
            ReleaseDate: ParseReleaseDate(dto.releaseDate),
            CoverArtId: dto.cover ?? string.Empty,
            IsAvailable: dto.streamReady);
    }

    private static TidalSearchResults MapToTidalSearchResults(TidalSearchResponseDto dto)
    {
        return new TidalSearchResults(
            Albums: dto.albums.items.Select(MapToTidalAlbumInfo).ToList(),
            Tracks: dto.tracks.items.Select(MapToTidalTrackInfo).ToList(),
            TotalCount: (dto.albums.items?.Count ?? 0) + (dto.tracks.items?.Count ?? 0),
            HasMore: false);
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
        var qualities = new List<TidalQuality> { TidalQuality.Low, TidalQuality.High, TidalQuality.Lossless };
        if (dto.audioQuality?.Contains("HI_RES", StringComparison.OrdinalIgnoreCase) == true)
            qualities.Add(TidalQuality.HiRes);
        return qualities;
    }

    private static DateTime ParseReleaseDate(string releaseDate)
    {
        return DateTime.TryParse(releaseDate, out var date) ? date : DateTime.MinValue;
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}
