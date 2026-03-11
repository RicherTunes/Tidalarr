namespace Tidalarr.Core.Models;

/// <summary>
/// Represents information about a Tidal album.
/// </summary>
public record TidalAlbumInfo(
    string Id,
    string Title,
    IReadOnlyList<string> Artists,
    IReadOnlyList<TidalTrackInfo> Tracks,
    IReadOnlyList<TidalQuality> AvailableQualities,
    DateTime ReleaseDate,
    string CoverArtId,
    bool IsAvailable,
    long? PrimaryArtistId = null);
