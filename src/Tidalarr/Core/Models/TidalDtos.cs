using System.Text.Json.Serialization;
using Tidalarr.Core.Serialization;

namespace Tidalarr.Core.Models;

/// <summary>
/// DTO for Tidal artist from API response.
/// Note: Tidal API typically returns numeric IDs, but some endpoints/markets may return strings.
/// FlexibleLongJsonConverter handles both cases for resilience.
/// </summary>
public class TidalArtistDto
{
    [JsonConverter(typeof(FlexibleLongJsonConverter))]
    public long id { get; set; }
    public string? name { get; set; }

    public TidalArtistDto() { }

    public TidalArtistDto(string? name, string? id)
    {
        this.name = name;
        this.id = long.TryParse(id, out var parsed) ? parsed : 0;
    }
}

/// <summary>
/// DTO for Tidal album from API response.
/// Note: Tidal API typically returns numeric IDs, but some endpoints/markets may return strings.
/// FlexibleLongJsonConverter handles both cases for resilience.
/// </summary>
public class TidalAlbumDto
{
    [JsonConverter(typeof(FlexibleLongJsonConverter))]
    public long id { get; set; }
    public string title { get; set; } = string.Empty;
    public TidalArtistDto? artist { get; set; }
    public List<TidalArtistDto>? artists { get; set; }
    public string? audioQuality { get; set; }
    public string? releaseDate { get; set; }
    public string? cover { get; set; }
    public bool streamReady { get; set; }
    public int numberOfTracks { get; set; }

    public TidalAlbumDto() { }

    public TidalAlbumDto(string id, string title, TidalArtistDto? artist, string? releaseDate, int numberOfTracks, int duration, bool streamReady, string? cover, string? audioQuality = null)
    {
        this.id = long.TryParse(id, out var parsed) ? parsed : 0;
        this.title = title;
        this.artist = artist;
        this.releaseDate = releaseDate;
        this.numberOfTracks = numberOfTracks;
        // duration parameter accepted for test compatibility but not stored (album DTO doesn't track duration)
        this.streamReady = streamReady;
        this.cover = cover;
        this.audioQuality = audioQuality;
    }
}

/// <summary>
/// DTO for Tidal track from API response.
/// Note: Tidal API typically returns numeric IDs, but some endpoints/markets may return strings.
/// FlexibleLongJsonConverter handles both cases for resilience.
/// </summary>
public class TidalTrackDto
{
    [JsonConverter(typeof(FlexibleLongJsonConverter))]
    public long id { get; set; }
    public string title { get; set; } = string.Empty;
    public TidalArtistDto? artist { get; set; }
    public List<TidalArtistDto>? artists { get; set; }
    public TidalAlbumDto? album { get; set; }
    public int trackNumber { get; set; }
    public int duration { get; set; }
    public string? audioQuality { get; set; }
    public bool streamReady { get; set; }
    public string? isrc { get; set; }

    public TidalTrackDto() { }

    public TidalTrackDto(string id, string title, TidalArtistDto? artist, TidalAlbumDto? album, int trackNumber, int duration, bool streamReady, string? audioQuality)
    {
        this.id = long.TryParse(id, out var parsed) ? parsed : 0;
        this.title = title;
        this.artist = artist;
        this.album = album;
        this.trackNumber = trackNumber;
        this.duration = duration;
        this.streamReady = streamReady;
        this.audioQuality = audioQuality;
    }
}

/// <summary>
/// DTO for paginated items from Tidal API.
/// </summary>
public class TidalPagedItemsDto<T>
{
    public List<T>? items { get; set; }
    public int totalNumberOfItems { get; set; }
    public int limit { get; set; }
    public int offset { get; set; }

    public TidalPagedItemsDto() { }

    public TidalPagedItemsDto(List<T>? items)
    {
        this.items = items;
        this.totalNumberOfItems = items?.Count ?? 0;
    }
}

/// <summary>
/// Type alias for album paged response (for test compatibility).
/// </summary>
public class TidalAlbumsResponseDto : TidalPagedItemsDto<TidalAlbumDto>
{
    public TidalAlbumsResponseDto() : base() { }
    public TidalAlbumsResponseDto(List<TidalAlbumDto>? items) : base(items) { }
}

/// <summary>
/// Type alias for track paged response (for test compatibility).
/// </summary>
public class TidalTracksResponseDto : TidalPagedItemsDto<TidalTrackDto>
{
    public TidalTracksResponseDto() : base() { }
    public TidalTracksResponseDto(List<TidalTrackDto>? items) : base(items) { }
}

/// <summary>
/// DTO for Tidal search response from API.
/// </summary>
public class TidalSearchResponseDto
{
    public TidalPagedItemsDto<TidalAlbumDto>? albums { get; set; }
    public TidalPagedItemsDto<TidalTrackDto>? tracks { get; set; }
    public TidalPagedItemsDto<TidalArtistDto>? artists { get; set; }

    public TidalSearchResponseDto() { }

    public TidalSearchResponseDto(TidalPagedItemsDto<TidalAlbumDto>? albums, TidalPagedItemsDto<TidalTrackDto>? tracks)
    {
        this.albums = albums;
        this.tracks = tracks;
    }
}

/// <summary>
/// DTO for Tidal playback info from API.
/// </summary>
public class TidalPlaybackInfoDto
{
    public long trackId { get; set; }
    public string? assetPresentation { get; set; }
    public string? audioQuality { get; set; }
    public string? audioMode { get; set; }
    public string? manifestMimeType { get; set; }
    public string? manifest { get; set; }
    public string? encryptionType { get; set; }
    public string? securityToken { get; set; }
    public double? albumPeakAmplitude { get; set; }
    public double? albumReplayGain { get; set; }
    public double? trackPeakAmplitude { get; set; }
    public double? trackReplayGain { get; set; }
    public int? bitDepth { get; set; }
    public int? sampleRate { get; set; }

    public TidalPlaybackInfoDto() { }

    public TidalPlaybackInfoDto(string? manifest, string? manifestMimeType, string? encryptionType, string? securityToken)
    {
        this.manifest = manifest;
        this.manifestMimeType = manifestMimeType;
        this.encryptionType = encryptionType;
        this.securityToken = securityToken;
    }
}

/// <summary>
/// DTO for Tidal album tracks response from API.
/// </summary>
public class TidalAlbumTracksDto
{
    public List<TidalTrackDto>? items { get; set; }
    public int totalNumberOfItems { get; set; }
    public int limit { get; set; }
    public int offset { get; set; }

    public TidalAlbumTracksDto() { }

    public TidalAlbumTracksDto(List<TidalTrackDto>? items, int totalNumberOfItems)
    {
        this.items = items;
        this.totalNumberOfItems = totalNumberOfItems;
    }
}

/// <summary>
/// DTO for Tidal token response from OAuth.
/// </summary>
public class TidalTokenResponse
{
    public string access_token { get; set; } = string.Empty;
    public string refresh_token { get; set; } = string.Empty;
    public string token_type { get; set; } = string.Empty;
    public int expires_in { get; set; }
    public string? user_id { get; set; }
    public string? countryCode { get; set; }
}
