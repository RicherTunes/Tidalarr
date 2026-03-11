using System.IO.Compression;
using System.Text;
using System.Globalization;
using System.Text.Json;
using Lidarr.Plugin.Common.Interfaces;
using Lidarr.Plugin.Common.Services.Http;
using Lidarr.Plugin.Common.Utilities;
using Tidalarr.Core.Constants;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Tidalarr.Infrastructure.Observability;

namespace Tidalarr.Domain.Api;

public class TidalApiClient(HttpClient httpClient, ITidalAuth authService, IStreamingResponseCache? cache = null) : ITidalCore, IDisposable
{
    private readonly HttpClient _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    private readonly StreamingApiRequestBuilder _requestBuilder = new(TidalConstants.API_V1_BASE);
    private readonly IStreamingResponseCache? _cache = cache;
    private readonly ITidalAuth _authService = authService ?? throw new ArgumentNullException(nameof(authService));
    private readonly Streaming.TidalManifestParser? _manifestParser;
    private readonly ILogger<TidalApiClient> _logger = NullLogger<TidalApiClient>.Instance;

    // Overload that allows DI to provide a manifest parser without breaking existing callers
    [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
    public TidalApiClient(HttpClient httpClient, ITidalAuth authService, Streaming.TidalManifestParser manifestParser, IStreamingResponseCache? cache = null, ILogger<TidalApiClient>? logger = null)
        : this(httpClient, authService, cache)
    {
        this._manifestParser = manifestParser;
        this._logger = logger ?? NullLogger<TidalApiClient>.Instance;
    }
    public async Task<TidalTrackInfo> GetTrackAsync(string trackId, CancellationToken cancellationToken = default)
    {
        TidalTokens tokens = await this._authService.GetValidTokensAsync();
        string endpoint = $"tracks/{trackId}";
        Dictionary<string, string> parameters = new()
        {
            ["sessionId"] = tokens.SessionId,
            ["countryCode"] = tokens.CountryCode
        };
        if (this._cache?.Get<TidalTrackDto>(endpoint, parameters) is { } cachedTrack)
        {
            return MapToTidalTrackInfo(cachedTrack);
        }

        HttpRequestMessage request = this._requestBuilder
            .Endpoint(endpoint)
            .QueryParams(parameters)
            .BearerToken(tokens.AccessToken)
            .WithStreamingDefaults("Tidalarr/1.0.0")
            .Build();
        System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
        using IDisposable scope = ObservabilityShim.StartApi(this._logger, service: "tidal", endpoint: endpoint);
        HttpResponseMessage response = await this._httpClient.ExecuteWithRetryAsync(request, cancellationToken: cancellationToken);
        sw.Stop();
        ObservabilityShim.CompleteApi(this._logger, service: "tidal", endpoint: endpoint, statusCode: (int)response.StatusCode, success: response.IsSuccessStatusCode, duration: sw.Elapsed);
        _ = response.EnsureSuccessStatusCode();
        string content = await ReadContentAsStringAsync(response, cancellationToken);
        TidalTrackDto? dto = JsonSerializer.Deserialize<TidalTrackDto>(content) ?? throw new InvalidOperationException("Failed to parse track response");
        this._cache?.Set(endpoint, parameters, dto, TimeSpan.FromHours(1));
        return MapToTidalTrackInfo(dto);
    }
    public async Task<TidalAlbumInfo> GetAlbumAsync(string albumId, CancellationToken cancellationToken = default)
    {
        TidalTokens tokens = await this._authService.GetValidTokensAsync();
        string endpoint = $"albums/{albumId}";
        Dictionary<string, string> parameters = new()
        {
            ["sessionId"] = tokens.SessionId,
            ["countryCode"] = tokens.CountryCode
        };
        if (this._cache?.Get<TidalAlbumDto>(endpoint, parameters) is { } cachedAlbum)
        {
            return MapToTidalAlbumInfo(cachedAlbum);
        }

        HttpRequestMessage request = this._requestBuilder
            .Endpoint(endpoint)
            .QueryParams(parameters)
            .BearerToken(tokens.AccessToken)
            .WithStreamingDefaults("Tidalarr/1.0.0")
            .Build();
        System.Diagnostics.Stopwatch sw2 = System.Diagnostics.Stopwatch.StartNew();
        using IDisposable scope2 = ObservabilityShim.StartApi(this._logger, service: "tidal", endpoint: endpoint);
        HttpResponseMessage response = await this._httpClient.ExecuteWithRetryAsync(request, cancellationToken: cancellationToken);
        sw2.Stop();
        ObservabilityShim.CompleteApi(this._logger, service: "tidal", endpoint: endpoint, statusCode: (int)response.StatusCode, success: response.IsSuccessStatusCode, duration: sw2.Elapsed);
        _ = response.EnsureSuccessStatusCode();
        string content = await ReadContentAsStringAsync(response, cancellationToken);
        TidalAlbumDto? dto = JsonSerializer.Deserialize<TidalAlbumDto>(content) ?? throw new InvalidOperationException("Failed to parse album response");
        this._cache?.Set(endpoint, parameters, dto, TimeSpan.FromHours(2));
        return MapToTidalAlbumInfo(dto);
    }
    public async Task<List<TidalTrackInfo>> GetAlbumTracksAsync(string albumId, CancellationToken cancellationToken = default)
    {
        TidalTokens tokens = await this._authService.GetValidTokensAsync();
        string endpoint = $"albums/{albumId}/tracks";
        Dictionary<string, string> parameters = new()
        {
            ["sessionId"] = tokens.SessionId,
            ["countryCode"] = tokens.CountryCode,
            ["limit"] = TidalConstants.DEFAULT_ITEM_LIMIT.ToString()
        };
        if (this._cache?.Get<TidalAlbumTracksDto>(endpoint, parameters) is { } cached)
        {
            return [.. (cached.items ?? []).Select(MapToTidalTrackInfo)];
        }

        HttpRequestMessage request = this._requestBuilder
            .Endpoint(endpoint)
            .QueryParams(parameters)
            .BearerToken(tokens.AccessToken)
            .WithStreamingDefaults("Tidalarr/1.0.0")
            .Build();
        System.Diagnostics.Stopwatch sw3 = System.Diagnostics.Stopwatch.StartNew();
        using IDisposable scope3 = ObservabilityShim.StartApi(this._logger, service: "tidal", endpoint: endpoint);
        HttpResponseMessage response = await this._httpClient.ExecuteWithRetryAsync(request, cancellationToken: cancellationToken);
        sw3.Stop();
        ObservabilityShim.CompleteApi(this._logger, service: "tidal", endpoint: endpoint, statusCode: (int)response.StatusCode, success: response.IsSuccessStatusCode, duration: sw3.Elapsed);
        _ = response.EnsureSuccessStatusCode();
        string content = await ReadContentAsStringAsync(response, cancellationToken);
        TidalAlbumTracksDto? dto = JsonSerializer.Deserialize<TidalAlbumTracksDto>(content) ?? throw new InvalidOperationException("Failed to parse album tracks response");
        if (dto.items == null)
        {
            throw new InvalidOperationException("Album tracks response missing items collection.");
        }

        this._cache?.Set(endpoint, parameters, dto, TimeSpan.FromHours(2));
        return [.. (dto.items ?? []).Select(MapToTidalTrackInfo)];
    }
    public async Task<TidalAlbumInfo> GetAlbumWithTracksAsync(string albumId, CancellationToken cancellationToken = default)
    {
        TidalAlbumInfo album = await GetAlbumAsync(albumId, cancellationToken);
        List<TidalTrackInfo> tracks = await GetAlbumTracksAsync(albumId, cancellationToken);
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
        TidalTokens tokens = await this._authService.GetValidTokensAsync();
        string endpoint = "search";
        Dictionary<string, string> parameters = new()
        {
            ["query"] = query,
            ["types"] = "albums,tracks",
            ["limit"] = limit.ToString(),
            ["sessionId"] = tokens.SessionId,
            ["countryCode"] = tokens.CountryCode
        };
        if (this._cache?.Get<TidalSearchResponseDto>(endpoint, parameters) is { } cached)
        {
            return MapToTidalSearchResults(cached);
        }

        HttpRequestMessage request = this._requestBuilder
            .Endpoint(endpoint)
            .QueryParams(parameters)
            .BearerToken(tokens.AccessToken)
            .WithStreamingDefaults("Tidalarr/1.0.0")
            .Build();
        System.Diagnostics.Stopwatch sw4 = System.Diagnostics.Stopwatch.StartNew();
        using IDisposable scope4 = ObservabilityShim.StartApi(this._logger, service: "tidal", endpoint: endpoint);
        HttpResponseMessage response = await this._httpClient.ExecuteWithRetryAsync(request, cancellationToken: cancellationToken);
        sw4.Stop();
        ObservabilityShim.CompleteApi(this._logger, service: "tidal", endpoint: endpoint, statusCode: (int)response.StatusCode, success: response.IsSuccessStatusCode, duration: sw4.Elapsed);
        _ = response.EnsureSuccessStatusCode();
        string content = await ReadContentAsStringAsync(response, cancellationToken);
        TidalSearchResponseDto? dto = JsonSerializer.Deserialize<TidalSearchResponseDto>(content) ?? throw new InvalidOperationException("Failed to parse search response");
        this._cache?.Set(endpoint, parameters, dto, TimeSpan.FromMinutes(5));
        return MapToTidalSearchResults(dto);
    }
    public async Task<TidalStreamInfo> GetStreamInfoAsync(string trackId, TidalQuality quality, CancellationToken cancellationToken = default)
    {
        TidalTokens tokens = await this._authService.GetValidTokensAsync();
        string endpoint = $"tracks/{trackId}/playbackinfopostpaywall";
        Dictionary<string, string> parameters = new()
        {
            ["audioquality"] = TidalConstants.QualityParameters[quality],
            ["playbackmode"] = "STREAM",
            ["assetpresentation"] = "FULL",
            ["sessionId"] = tokens.SessionId,
            ["countryCode"] = tokens.CountryCode
        };
        HttpRequestMessage request = this._requestBuilder
            .Endpoint(endpoint)
            .QueryParams(parameters)
            .BearerToken(tokens.AccessToken)
            .WithStreamingDefaults("Tidalarr/1.0.0")
            .Build();
        HttpResponseMessage response = await this._httpClient.ExecuteWithRetryAsync(request, cancellationToken: cancellationToken);
        _ = response.EnsureSuccessStatusCode();
        string content = await ReadContentAsStringAsync(response, cancellationToken);
        TidalPlaybackInfoDto? dto = JsonSerializer.Deserialize<TidalPlaybackInfoDto>(content) ?? throw new InvalidOperationException("Failed to parse playback info");
        string? encryptionType = dto.encryptionType;
        bool isEncrypted = !string.IsNullOrWhiteSpace(encryptionType) && !string.Equals(encryptionType, "NONE", StringComparison.OrdinalIgnoreCase);
        string? securityToken = dto.securityToken;
        // If a manifest parser is available, parse chunk URLs and better infer extension
        if (this._manifestParser != null && !string.IsNullOrEmpty(dto.manifest) && !string.IsNullOrEmpty(dto.manifestMimeType))
        {
            try
            {
                TidalManifest parsed = this._manifestParser.ParseManifest(dto.manifest, dto.manifestMimeType);
                TidalManifest manifest = parsed with { IsEncrypted = isEncrypted, SecurityToken = securityToken };
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
            ChunkUrls: [],
            FileExtension: InferPlaybackExtension(dto),
            MimeType: dto.manifestMimeType ?? string.Empty,
            IsEncrypted: isEncrypted,
            SecurityToken: securityToken);
    }
    // Raw playback-info fetch used by stream service for manifest parsing
    public async Task<TidalPlaybackInfoDto> GetPlaybackInfoAsync(string trackId, TidalQuality quality, CancellationToken cancellationToken = default)
    {
        TidalTokens tokens = await this._authService.GetValidTokensAsync();
        string endpoint = $"tracks/{trackId}/playbackinfopostpaywall";
        Dictionary<string, string> parameters = new()
        {
            ["audioquality"] = TidalConstants.QualityParameters[quality],
            ["playbackmode"] = "STREAM",
            ["assetpresentation"] = "FULL",
            ["sessionId"] = tokens.SessionId,
            ["countryCode"] = tokens.CountryCode
        };
        HttpRequestMessage request = this._requestBuilder
            .Endpoint(endpoint)
            .QueryParams(parameters)
            .BearerToken(tokens.AccessToken)
            .WithStreamingDefaults("Tidalarr/1.0.0")
            .Build();
        HttpResponseMessage response = await this._httpClient.SendAsync(request, cancellationToken);
        _ = response.EnsureSuccessStatusCode();
        string content = await ReadContentAsStringAsync(response, cancellationToken);
        TidalPlaybackInfoDto? dto = JsonSerializer.Deserialize<TidalPlaybackInfoDto>(content);
        return dto ?? throw new InvalidOperationException("Failed to parse playback info");
    }
    public async Task<bool> IsAuthenticatedAsync()
    {
        try
        {
            TidalTokens tokens = await this._authService.GetValidTokensAsync();
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
        {
            throw new InvalidOperationException("Track response missing album information.");
        }


        List<string> artistNames = [];
        if (!string.IsNullOrWhiteSpace(dto.artist?.name))
        {
            artistNames.Add(dto.artist!.name!);
        }

        if (dto.artists != null)
        {
            foreach (TidalArtistDto artist in dto.artists)
            {
                if (!string.IsNullOrWhiteSpace(artist?.name) && !artistNames.Contains(artist.name, StringComparer.OrdinalIgnoreCase))
                {
                    artistNames.Add(artist!.name!);
                }
            }
        }
        if (artistNames.Count == 0)
        {
            artistNames.Add("Unknown Artist");
        }

        string albumId = dto.album?.id.ToString() ?? string.Empty;
        string albumTitle = dto.album?.title ?? string.Empty;
        // Extract primary artist ID when available (> 0)
        long? primaryArtistId = dto.artist?.id > 0 ? dto.artist.id : null;
        return new TidalTrackInfo(
            Id: dto.id.ToString(),
            Title: dto.title,
            Artists: artistNames,
            AlbumId: albumId,
            AlbumTitle: albumTitle,
            TrackNumber: dto.trackNumber,
            Duration: dto.duration,
            Quality: MapQualityFromString(dto.audioQuality ?? string.Empty),
            IsAvailable: dto.streamReady,
            ReleaseDate: ParseReleaseDate(dto.album?.releaseDate),
            PrimaryArtistId: primaryArtistId);
    }
    private static TidalAlbumInfo MapToTidalAlbumInfo(TidalAlbumDto dto)  
    {
        // Note: Search results only have 'artists' array, not singular 'artist' field
        // The singular 'artist' field is only present in album detail responses
        List<string> artistNames = [];

        // First try singular artist (from album detail responses)
        if (!string.IsNullOrWhiteSpace(dto.artist?.name))
        {
            artistNames.Add(dto.artist!.name!);
        }

        // Then add from artists array (from search results and detail responses)
        if (dto.artists != null)
        {
            foreach (TidalArtistDto artist in dto.artists)
            {
                if (!string.IsNullOrWhiteSpace(artist?.name) && !artistNames.Contains(artist.name, StringComparer.OrdinalIgnoreCase))
                {
                    artistNames.Add(artist!.name!);
                }
            }
        }

        if (artistNames.Count == 0)
        {
            artistNames.Add("Unknown Artist");
        }
        // Extract primary artist ID when available (> 0)
        long? primaryArtistId = dto.artist?.id > 0 ? dto.artist.id : null;
        return new TidalAlbumInfo(
            Id: dto.id.ToString(),
            Title: dto.title,
            Artists: artistNames,
            Tracks: [],
            AvailableQualities: DetectAlbumQualities(dto),
            ReleaseDate: ParseReleaseDate(dto.releaseDate),
            CoverArtId: dto.cover ?? string.Empty,
            IsAvailable: dto.streamReady,
            PrimaryArtistId: primaryArtistId);
    }
    private static TidalSearchResults MapToTidalSearchResults(TidalSearchResponseDto dto)
    {
        // Gracefully handle partial responses — Tidal may omit collections
        List<TidalAlbumDto> albumDtos = dto.albums?.items ?? [];
        List<TidalTrackDto> trackDtos = dto.tracks?.items ?? [];
        List<TidalArtistDto> artistDtos = dto.artists?.items ?? [];
        return new TidalSearchResults(
            Albums: [.. albumDtos.Select(MapToTidalAlbumInfo)],
            Tracks: [.. trackDtos.Select(MapToTidalTrackInfo)],
            Artists: [.. artistDtos.Select(MapToTidalArtistInfo)],
            TotalCount: albumDtos.Count + trackDtos.Count + artistDtos.Count,
            HasMore: false);
    }

    private static TidalArtistInfo MapToTidalArtistInfo(TidalArtistDto dto)
    {
        return new TidalArtistInfo(
            Id: dto.id.ToString(),
            Name: dto.name ?? string.Empty,
            PictureId: null,
            AlbumCount: null,
            Url: null);
    }
    private static DateTime ParseReleaseDate(string? releaseDate)
    {
        return string.IsNullOrWhiteSpace(releaseDate)
            ? DateTime.MinValue
            : DateTime.TryParse(releaseDate, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTime date)
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
        // Parse audioQuality field to determine available qualities
        // Tidal API returns the maximum quality available (e.g., "HI_RES_LOSSLESS", "LOSSLESS", "HIGH")
        // We include all qualities up to and including the maximum
        TidalQuality maxQuality = MapQualityFromString(dto.audioQuality ?? string.Empty);

        // Build list of all qualities up to and including maxQuality
        // Tidal typically makes all lower qualities available when a higher quality exists
        List<TidalQuality> qualities = [];
        foreach (TidalQuality q in Enum.GetValues(typeof(TidalQuality)))
        {
            if (q <= maxQuality)
            {
                qualities.Add(q);
            }
        }

        // Ensure at least basic qualities are available (fallback for empty/null audioQuality)
        if (qualities.Count == 0)
        {
            qualities = [TidalQuality.Low, TidalQuality.High];
        }

        return qualities;
    }
    private static string InferPlaybackExtension(TidalPlaybackInfoDto dto)
    {
        string mime = dto.manifestMimeType ?? string.Empty;
        return mime.Contains("mp4", StringComparison.OrdinalIgnoreCase) || mime.Contains("mpeg", StringComparison.OrdinalIgnoreCase)
            ? ".m4a"
            : mime.Contains("flac", StringComparison.OrdinalIgnoreCase) || mime.Contains("wav", StringComparison.OrdinalIgnoreCase)
            ? ".flac"
            : ".m4a";
    }
    private static async Task<string> ReadContentAsStringAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.Content == null)
        {
            return string.Empty;
        }
        await using Stream responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using MemoryStream buffer = new();
        await responseStream.CopyToAsync(buffer, 81920, cancellationToken).ConfigureAwait(false);
        byte[] bytes = buffer.ToArray();
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
        using MemoryStream compressed = new(bytes);
        using Stream decompressed = streamFactory(compressed);
        using StreamReader reader = new(decompressed, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: false);
        return reader.ReadToEnd();
    }
    private static bool LooksLikeGzip(IReadOnlyList<byte> bytes)
    {
        return bytes.Count >= 2 && bytes[0] == 0x1F && bytes[1] == 0x8B;
    }

    private static bool LooksLikeZlib(IReadOnlyList<byte> bytes)
    {
        return bytes.Count >= 2 && bytes[0] == 0x78 && (bytes[1] == 0x01 || bytes[1] == 0x9C || bytes[1] == 0xDA);
    }

    private static bool HasEncoding(HttpResponseMessage response, string encoding)
    {
        return response.Content?.Headers?.ContentEncoding?.Any(e => string.Equals(e, encoding, StringComparison.OrdinalIgnoreCase)) == true;
    }

    public void Dispose()
    {
        this._httpClient?.Dispose();
    }
}
