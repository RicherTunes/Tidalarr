namespace Tidalarr.Core.Models;

/// <summary>
/// Represents search results from Tidal.
/// </summary>
public record TidalSearchResults(
    IReadOnlyList<TidalAlbumInfo> Albums,
    IReadOnlyList<TidalTrackInfo> Tracks,
    IReadOnlyList<TidalArtistInfo> Artists,
    int TotalCount,
    bool HasMore);
