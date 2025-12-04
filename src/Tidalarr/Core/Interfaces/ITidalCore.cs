using Tidalarr.Core.Models;

namespace Tidalarr.Core.Interfaces;

public interface ITidalCore
{
    Task<TidalTrackInfo> GetTrackAsync(string trackId, CancellationToken cancellationToken = default);
    Task<TidalAlbumInfo> GetAlbumAsync(string albumId, CancellationToken cancellationToken = default);
    Task<List<TidalTrackInfo>> GetAlbumTracksAsync(string albumId, CancellationToken cancellationToken = default);
    Task<TidalAlbumInfo> GetAlbumWithTracksAsync(string albumId, CancellationToken cancellationToken = default);
    Task<TidalSearchResults> SearchAsync(string query, int limit = 100, CancellationToken cancellationToken = default);
    Task<TidalStreamInfo> GetStreamInfoAsync(string trackId, TidalQuality quality, CancellationToken cancellationToken = default);
    Task<bool> IsAuthenticatedAsync();

    // New scalable surface: raw playback info fetch with manifest and mime
    // Default implementation throws to avoid breaking existing stubs; concrete clients should override.
    Task<TidalPlaybackInfoDto> GetPlaybackInfoAsync(string trackId, TidalQuality quality, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Playback-info is not supported by this ITidalCore implementation");
    }
}

