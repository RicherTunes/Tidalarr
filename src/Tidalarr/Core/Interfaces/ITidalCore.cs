using Tidalarr.Core.Models;

namespace Tidalarr.Core.Interfaces;

public interface ITidalCore
{
    Task<TidalTrackInfo> GetTrackAsync(string trackId, CancellationToken cancellationToken = default);
    Task<TidalAlbumInfo> GetAlbumAsync(string albumId, CancellationToken cancellationToken = default);
    Task<List<TidalTrackInfo>> GetAlbumTracksAsync(string albumId, CancellationToken cancellationToken = default);
    Task<TidalAlbumInfo> GetAlbumWithTracksAsync(string albumId, CancellationToken cancellationToken = default);
    Task<TidalSearchResults> SearchAsync(string query, int limit = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches Tidal with an explicit country-code override for market filtering.
    /// The <paramref name="countryCode"/> is sent as the <c>countryCode</c> query parameter
    /// to the Tidal API, overriding the token's default country when non-null.
    /// Default implementation ignores countryCode and delegates to the base overload
    /// so existing implementors are not broken.
    /// </summary>
    Task<TidalSearchResults> SearchAsync(string query, int limit, string? countryCode, CancellationToken cancellationToken = default)
    {
        // Default: ignore countryCode and delegate to the base method.
        // Concrete clients (e.g. TidalApiClient) should override to pass countryCode to the API.
        return SearchAsync(query, limit, cancellationToken);
    }

    Task<TidalStreamInfo> GetStreamInfoAsync(string trackId, TidalQuality quality, CancellationToken cancellationToken = default);
    Task<bool> IsAuthenticatedAsync();

    /// <summary>
    /// Fetches every album in the authenticated user's Tidal favorites
    /// (<c>users/{userId}/favorites/albums</c>), paging through the full collection.
    /// Requires a session with a non-empty <see cref="TidalTokens.UserId"/>.
    /// Default implementation throws so existing stubs are not broken; concrete clients override.
    /// </summary>
    Task<List<TidalAlbumInfo>> GetFavoriteAlbumsAsync(CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Favorite albums are not supported by this ITidalCore implementation");

    /// <summary>
    /// Fetches every artist in the authenticated user's Tidal favorites
    /// (<c>users/{userId}/favorites/artists</c>), paging through the full collection.
    /// Requires a session with a non-empty <see cref="TidalTokens.UserId"/>.
    /// Default implementation throws so existing stubs are not broken; concrete clients override.
    /// </summary>
    Task<List<TidalArtistInfo>> GetFavoriteArtistsAsync(CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Favorite artists are not supported by this ITidalCore implementation");

    // New scalable surface: raw playback info fetch with manifest and mime
    // Default implementation throws to avoid breaking existing stubs; concrete clients should override.
    Task<TidalPlaybackInfoDto> GetPlaybackInfoAsync(string trackId, TidalQuality quality, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Playback-info is not supported by this ITidalCore implementation");
    }
}

