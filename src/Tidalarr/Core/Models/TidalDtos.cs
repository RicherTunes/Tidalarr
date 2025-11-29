namespace Tidalarr.Core.Models;

/// <summary>
/// DTO for Tidal artist from API response.
/// </summary>
public class TidalArtistDto
{
    public string? id { get; set; }
    public string? name { get; set; }
}

/// <summary>
/// DTO for Tidal album from API response.
/// </summary>
public class TidalAlbumDto
{
    public string id { get; set; } = string.Empty;
    public string title { get; set; } = string.Empty;
    public TidalArtistDto? artist { get; set; }
    public List<TidalArtistDto>? artists { get; set; }
    public string? audioQuality { get; set; }
    public string? releaseDate { get; set; }
    public string? cover { get; set; }
    public bool streamReady { get; set; }
    public int numberOfTracks { get; set; }
}

/// <summary>
/// DTO for Tidal track from API response.
/// </summary>
public class TidalTrackDto
{
    public string id { get; set; } = string.Empty;
    public string title { get; set; } = string.Empty;
    public TidalArtistDto? artist { get; set; }
    public List<TidalArtistDto>? artists { get; set; }
    public TidalAlbumDto? album { get; set; }
    public int trackNumber { get; set; }
    public int duration { get; set; }
    public string? audioQuality { get; set; }
    public bool streamReady { get; set; }
    public string? isrc { get; set; }
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
}

/// <summary>
/// DTO for Tidal search response from API.
/// </summary>
public class TidalSearchResponseDto
{
    public TidalPagedItemsDto<TidalAlbumDto>? albums { get; set; }
    public TidalPagedItemsDto<TidalTrackDto>? tracks { get; set; }
    public TidalPagedItemsDto<TidalArtistDto>? artists { get; set; }
}

/// <summary>
/// DTO for Tidal playback info from API.
/// </summary>
public class TidalPlaybackInfoDto
{
    public string? trackId { get; set; }
    public string? assetPresentation { get; set; }
    public string? audioQuality { get; set; }
    public string? audioMode { get; set; }
    public string? manifestMimeType { get; set; }
    public string? manifest { get; set; }
    public string? encryptionType { get; set; }
    public string? securityToken { get; set; }
    public int? albumPeakAmplitude { get; set; }
    public int? albumReplayGain { get; set; }
    public int? trackPeakAmplitude { get; set; }
    public int? trackReplayGain { get; set; }
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
