using Tidalarr.Core.Models;

namespace Tidalarr.Domain.Filters;

/// <summary>
/// Clips pre-release albums whose release date is further in the future than
/// the user-configured EarlyReleaseLimit (in days).
///
/// Background: Lidarr's typical use of an "early download" window is to grab a
/// release the moment it drops, optionally allowing pre-orders a few days
/// out. Tidalarr exposes <c>EarlyReleaseLimit</c> on its indexer settings
/// (default: 14 days). Before this filter, the setting was stored but never
/// applied — search results included albums with release dates years in the
/// future, which Lidarr would then try to import.
///
/// Semantics:
/// <list type="bullet">
///   <item>If the limit is <c>null</c>, no filtering is applied.</item>
///   <item>If the limit is negative, it is clamped to <c>0</c> (no future releases at all).</item>
///   <item>Albums with a release date today or earlier are always included.</item>
///   <item>Albums with a release date up to and including <c>utcNow + limit days</c> are included.</item>
///   <item>Albums beyond that window are excluded.</item>
/// </list>
///
/// Pure function: takes a clock parameter so tests don't need to wait for time
/// to pass and don't depend on the system clock.
/// </summary>
public static class EarlyReleaseFilter
{
    /// <summary>
    /// Returns a new list containing only the albums whose <see cref="TidalAlbumInfo.ReleaseDate"/>
    /// is within the allowed window (or already past).
    /// </summary>
    /// <param name="albums">Candidate albums (typically search results).</param>
    /// <param name="earlyReleaseLimitDays">User-configured maximum days into the future. <c>null</c> disables filtering.</param>
    /// <param name="utcNow">Current UTC time, supplied for testability.</param>
    public static IReadOnlyList<TidalAlbumInfo> Filter(
        IReadOnlyList<TidalAlbumInfo> albums,
        int? earlyReleaseLimitDays,
        DateTimeOffset utcNow)
    {
        if (albums is null || albums.Count == 0) return albums ?? Array.Empty<TidalAlbumInfo>();
        if (!earlyReleaseLimitDays.HasValue) return albums;

        // Clamp negative inputs — defensive against weird settings or migration glitches.
        int limit = Math.Max(0, earlyReleaseLimitDays.Value);
        DateTime cutoff = utcNow.UtcDateTime.Date.AddDays(limit);

        // Compare date-only to avoid timezone fence-post problems for releases that
        // were sourced at midnight in some other timezone. The release timestamp itself
        // is interpreted in UTC because that's what TidalApiClient maps from the API.
        var filtered = new List<TidalAlbumInfo>(albums.Count);
        foreach (var album in albums)
        {
            if (album.ReleaseDate.Date <= cutoff)
            {
                filtered.Add(album);
            }
        }
        return filtered;
    }
}
