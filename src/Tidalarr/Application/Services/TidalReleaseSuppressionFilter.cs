using System.Collections.Generic;
using System.Linq;
using NLog;
using Tidalarr.Core.Models;

namespace Tidalarr.Application.Services;

/// <summary>
/// Parser-side terminal-release suppression gate. A suppressed album is withheld from AUTOMATIC/RSS
/// searches (the re-grab-loop driver) but OFFERED on an INTERACTIVE (user-initiated) search so a user can
/// recover a previously-restricted album — e.g. after a region change or subscription upgrade — without
/// waiting out the 30-day suppression TTL. Mirrors qobuz's <c>QobuzParser.ConvertAlbumToReleases</c>
/// interactive-override behavior, adapted to Tidal's album-list architecture.
///
/// <para>This is a pure, host-free function so both release-emission paths
/// (<c>TidalLidarrIndexer.FetchReleases</c> and <c>TidalLidarrParser.ParseResponse</c>) share one tested
/// implementation and it is unit-testable without Lidarr's indexer base class.</para>
/// </summary>
public static class TidalReleaseSuppressionFilter
{
    public static IReadOnlyList<TidalAlbumInfo> Apply(
        IReadOnlyList<TidalAlbumInfo> albums,
        ITidalReleaseSuppressionStore? store,
        bool isInteractiveSearch,
        Logger? logger = null)
    {
        if (albums is null || albums.Count == 0)
        {
            return albums ?? new List<TidalAlbumInfo>();
        }

        // No store, or an interactive (user-initiated) search — offer everything. Interactive is an
        // explicit "I want this now", so it bypasses suppression entirely. If the album still can't be
        // satisfied, the next download re-suppresses it after one bounded cycle; automatic searches keep
        // respecting suppression, so the loop stays stopped.
        if (store is null || isInteractiveSearch)
        {
            return albums;
        }

        var kept = new List<TidalAlbumInfo>(albums.Count);
        foreach (var album in albums)
        {
            // A blank id can never be a suppression key — pass it through untouched.
            if (album is null || string.IsNullOrWhiteSpace(album.Id) || !store.IsSuppressed(album.Id))
            {
                if (album is not null)
                {
                    kept.Add(album);
                }
            }
            else
            {
                logger?.Debug(
                    "Withholding suppressed Tidal album from automatic search (previously failed on a permanently-unavailable track): {0} - {1}",
                    album.Artists?.FirstOrDefault() ?? "Unknown Artist", album.Title);
            }
        }

        return kept;
    }
}
