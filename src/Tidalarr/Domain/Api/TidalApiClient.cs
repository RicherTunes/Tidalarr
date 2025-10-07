using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Lidarr.Plugin.Common.Interfaces;
using Lidarr.Plugin.Common.Services.Caching;
using Lidarr.Plugin.Common.Services.Http;
using Lidarr.Plugin.Common.Utilities;
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
        _requestBuilder = new StreamingApiRequestBuilder(TidalConstants.API_V1_BASE)
            .Header("X-Tidal-Token", TidalConstants.CLIENT_ID);
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
        var response = await _httpClient.ExecuteWithResilienceAsync(request, cancellationToken: cancellationToken);
        response.EnsureSuccessStatusCode();
        var content = await ReadContentAsStringAsync(response, cancellationToken);
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
        var response = await _httpClient.ExecuteWithResilienceAsync(request, cancellationToken: cancellationToken);
        response.EnsureSuccessStatusCode();
        var content = await ReadContentAsStringAsync(response, cancellationToken);
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
            return (cached.items ?? new List<TidalTrackDto>()).Select(MapToTidalTrackInfo).ToList();
        var request = _requestBuilder
            .Endpoint(endpoint)
            .QueryParams(parameters)
            .BearerToken(tokens.AccessToken)
            .WithStreamingDefaults("Tidalarr/1.0.0")
            .Build();
        var response = await _httpClient.ExecuteWithResilienceAsync(request, cancellationToken: cancellationToken);
        response.EnsureSuccessStatusCode();
        var content = await ReadContentAsStringAsync(response, cancellationToken);
        var dto = JsonSerializer.Deserialize<TidalAlbumTracksDto>(content);
        if (dto == null) throw new InvalidOperationException("Failed to parse album tracks response");
        if (dto.items == null)
            throw new InvalidOperationException("Album tracks response missing items collection.");
        _cache?.Set(endpoint, parameters, dto, TimeSpan.FromHours(2));
        return (dto.items ?? new List<TidalTrackDto>()).Select(MapToTidalTrackInfo).ToList();
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
        var response = await _httpClient.ExecuteWithResilienceAsync(request, cancellationToken: cancellationToken);
        response.EnsureSuccessStatusCode();
        var content = await ReadContentAsStringAsync(response, cancellationToken);
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
        var response = await _httpClient.ExecuteWithResilienceAsync(request, cancellationToken: cancellationToken);
        response.EnsureSuccessStatusCode();
        var content = await ReadContentAsStringAsync(response, cancellationToken);
        var dto = JsonSerializer.Deserialize<TidalPlaybackInfoDto>(content);
        if (dto == null) throw new InvalidOperationException("Failed to parse playback info");
        var encryptionType = dto.encryptionType;
        var isEncrypted = !string.IsNullOrWhiteSpace(encryptionType) && !string.Equals(encryptionType, "NONE", StringComparison.OrdinalIgnoreCase);
        var securityToken = dto.securityToken;
        // If a manifest parser is available, parse chunk URLs and better infer extension
        if (_manifestParser != null && !string.IsNullOrEmpty(dto.manifest) && !string.IsNullOrEmpty(dto.manifestMimeType))
        {
            try
            {
                var parsed = _manifestParser.ParseManifest(dto.manifest, dto.manifestMimeType);
                var manifest = parsed with { IsEncrypted = isEncrypted, SecurityToken = securityToken };
                return new TidalStreamInfo(
                    TrackId: trackId,
                    ChunkUrls: manifest.ChunkUrls,
                    FileExtension: manifest.FileExtension,
                    MimeType: manifest.MimeType,
                    IsEncrypted: manifest.IsEncrypted,
                    SecurityToken: manifest.SecurityToken);
            }
            catch
            {
                // Fallback to legacy behavior below
            }
        }
        return new TidalStreamInfo(
            TrackId: trackId,
            ChunkUrls: Array.Empty<string>(),
            FileExtension: InferPlaybackExtension(dto),
            MimeType: dto.manifestMimeType ?? string.Empty,
            IsEncrypted: isEncrypted,
            SecurityToken: securityToken);
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
        var content = await ReadContentAsStringAsync(response, cancellationToken);
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
        if (dto.album == null)
            throw new InvalidOperationException("Track response missing album information.");
        var artistNames = new List<string>();
        if (!string.IsNullOrWhiteSpace(dto.artist?.name))
            artistNames.Add(dto.artist!.name!);
        if (dto.artists != null)
        {
            foreach (var artist in dto.artists)
            {
                if (!string.IsNullOrWhiteSpace(artist?.name))
                    artistNames.Add(artist!.name!);
            }
        }
        if (artistNames.Count == 0)
            artistNames.Add("Unknown Artist");
        var albumId = dto.album?.id ?? string.Empty;
        var albumTitle = dto.album?.title ?? string.Empty;
        return new TidalTrackInfo(
            Id: dto.id,
            Title: dto.title,
            Artists: artistNames,
            AlbumId: albumId,
            AlbumTitle: albumTitle,
            TrackNumber: dto.trackNumber,
            Duration: dto.duration,
            Quality: MapQualityFromString(dto.audioQuality ?? string.Empty),
            IsAvailable: dto.streamReady,
            ReleaseDate: ParseReleaseDate(dto.album?.releaseDate));
    }
    private static TidalAlbumInfo MapToTidalAlbumInfo(TidalAlbumDto dto)
    {
        if (dto.artist == null)
            throw new InvalidOperationException("Album response missing primary artist.");
        var artistNames = new List<string>();
        if (!string.IsNullOrWhiteSpace(dto.artist?.name))
            artistNames.Add(dto.artist!.name!);
        if (dto.artists != null)
        {
            foreach (var artist in dto.artists)
            {
                if (!string.IsNullOrWhiteSpace(artist?.name))
                    artistNames.Add(artist!.name!);
            }
        }
        if (artistNames.Count == 0)
            artistNames.Add("Unknown Artist");
        return new TidalAlbumInfo(
            Id: dto.id,
            Title: dto.title,
            Artists: artistNames,
            Tracks: new List<TidalTrackInfo>(),
            AvailableQualities: DetectAlbumQualities(dto),
            ReleaseDate: ParseReleaseDate(dto.releaseDate),
            CoverArtId: dto.cover ?? string.Empty,
            IsAvailable: dto.streamReady);
    }
    private static TidalSearchResults MapToTidalSearchResults(TidalSearchResponseDto dto)
    {
        if (dto.albums?.items == null || dto.tracks?.items == null)
            throw new ArgumentNullException(nameof(dto), "Search response missing album or track collections.");
        var albumDtos = dto.albums?.items ?? new List<TidalAlbumDto>();
        var trackDtos = dto.tracks?.items ?? new List<TidalTrackDto>();
        return new TidalSearchResults(
            Albums: albumDtos.Select(MapToTidalAlbumInfo).ToList(),
            Tracks: trackDtos.Select(MapToTidalTrackInfo).ToList(),
            TotalCount: albumDtos.Count + trackDtos.Count,
            HasMore: false);
    }
    private static DateTime ParseReleaseDate(string? releaseDate)
    {
        if (string.IsNullOrWhiteSpace(releaseDate))
            return DateTime.MinValue;
        return DateTime.TryParse(releaseDate, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date)
            ? date
            : DateTime.MinValue;
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
    private static string InferPlaybackExtension(TidalPlaybackInfoDto dto)
    {
        var mime = dto.manifestMimeType ?? string.Empty;
        if (mime.Contains("mp4", StringComparison.OrdinalIgnoreCase) || mime.Contains("mpeg", StringComparison.OrdinalIgnoreCase))
            return ".m4a";
        if (mime.Contains("flac", StringComparison.OrdinalIgnoreCase) || mime.Contains("wav", StringComparison.OrdinalIgnoreCase))
            return ".flac";
        return ".m4a";
    }
    private static async Task<string> ReadContentAsStringAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.Content == null)
        {
            return string.Empty;
        }
        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        await responseStream.CopyToAsync(buffer, 81920, cancellationToken).ConfigureAwait(false);
        var bytes = buffer.ToArray();
        return DecodeResponseBody(bytes, response);
    }
    private static string DecodeResponseBody(byte[] bytes, HttpResponseMessage response)
    {
        if (bytes.Length == 0)
        {
            return string.Empty;
        }
        try
        {
            if (LooksLikeGzip(bytes) || HasEncoding(response, "gzip"))
            {
                return Decompress(bytes, stream => new GZipStream(stream, CompressionMode.Decompress, leaveOpen: false));
            }
            if (LooksLikeZlib(bytes) || HasEncoding(response, "deflate"))
            {
                return Decompress(bytes, stream => new DeflateStream(stream, CompressionMode.Decompress, leaveOpen: false));
            }
            if (HasEncoding(response, "br"))
            {
                return Decompress(bytes, stream => new BrotliStream(stream, CompressionMode.Decompress, leaveOpen: false));
            }
        }
        catch (InvalidDataException)
        {
            // Fall back to UTF-8 decode below
        }
        catch (IOException)
        {
            // Fall back to UTF-8 decode below
        }
        return Encoding.UTF8.GetString(bytes);
    }
    private static string Decompress(byte[] bytes, Func<Stream, Stream> streamFactory)
    {
        using var compressed = new MemoryStream(bytes);
        using var decompressed = streamFactory(compressed);
        using var reader = new StreamReader(decompressed, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: false);
        return reader.ReadToEnd();
    }
    private static bool LooksLikeGzip(IReadOnlyList<byte> bytes)
        => bytes.Count >= 2 && bytes[0] == 0x1F && bytes[1] == 0x8B;
    private static bool LooksLikeZlib(IReadOnlyList<byte> bytes)
        => bytes.Count >= 2 && bytes[0] == 0x78 && (bytes[1] == 0x01 || bytes[1] == 0x9C || bytes[1] == 0xDA);
    private static bool HasEncoding(HttpResponseMessage response, string encoding)
        => response.Content?.Headers?.ContentEncoding?.Any(e => string.Equals(e, encoding, StringComparison.OrdinalIgnoreCase)) == true;
    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}

