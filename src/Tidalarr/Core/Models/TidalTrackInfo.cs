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
    DateTime ReleaseDate,
    long? PrimaryArtistId = null)
{
    /// <summary>
    /// ISRC (International Standard Recording Code) when the catalog provides it. Kept out of the
    /// positional parameter list so the auto-generated Deconstruct stays 11-arity (existing
    /// deconstructions/cov tests unaffected). Written to file tags to anchor Lidarr import matching.
    /// </summary>
    public string Isrc { get; init; } = string.Empty;
}
