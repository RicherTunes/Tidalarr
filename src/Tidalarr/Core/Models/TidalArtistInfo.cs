namespace Tidalarr.Core.Models;

/// <summary>
/// Represents information about a Tidal artist.
/// </summary>
public record TidalArtistInfo(
    string Id,
    string Name,
    string? PictureId,
    int? AlbumCount,
    string? Url);
