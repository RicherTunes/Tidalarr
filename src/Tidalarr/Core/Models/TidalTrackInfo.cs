namespace Tidalarr.Core.Models;

/// <summary>
/// Represents information about a Tidal track.
/// </summary>
public record TidalTrackInfo(
    string Id,
    string Title,
    IReadOnlyList<string> Artists,
    string AlbumId,
    string AlbumTitle,
    int TrackNumber,
    int Duration,
    TidalQuality Quality,
    bool IsAvailable,
    DateTime ReleaseDate);
