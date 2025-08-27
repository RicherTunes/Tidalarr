using Tidalarr.Core.Models;

namespace Tidalarr.Core.Interfaces;

public interface ITidalCore
{
    Task<TidalTrackInfo> GetTrackAsync(string trackId, CancellationToken cancellationToken = default);
    Task<TidalAlbumInfo> GetAlbumAsync(string albumId, CancellationToken cancellationToken = default);
    Task<TidalSearchResults> SearchAsync(string query, int limit = 100, CancellationToken cancellationToken = default);
    Task<TidalStreamInfo> GetStreamInfoAsync(string trackId, TidalQuality quality, CancellationToken cancellationToken = default);
}
