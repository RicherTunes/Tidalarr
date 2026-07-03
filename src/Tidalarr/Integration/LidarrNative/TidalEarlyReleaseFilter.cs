using Tidalarr.Core.Models;

namespace Tidalarr.Integration.LidarrNative;

/// <summary>
/// Gives the "Early Release Limit" setting (<see cref="TidalLidarrIndexerSettings.EarlyReleaseLimit"/> /
/// <see cref="Tidalarr.Integration.TidalIndexerSettings.EarlyReleaseLimit"/>) a real runtime effect.
///
/// T-3 (external dead-settings audit): this value was accepted, validated (0-365 days), and copied
/// between settings DTOs across the plugin, but no code ever read it to decide whether an album
/// should be surfaced — Tidal listings with a release date arbitrarily far in the future were always
/// returned. This filter excludes albums whose release date is further than the configured window
/// away, matching the documented behaviour ("Skip pre-release downloads beyond this many days before
/// release").
/// </summary>
internal static class TidalEarlyReleaseFilter
{
    /// <summary>
    /// Filters <paramref name="albums"/>, dropping any whose <see cref="TidalAlbumInfo.ReleaseDate"/>
    /// is more than <paramref name="earlyReleaseLimitDays"/> days after <paramref name="utcNow"/>.
    /// Already-released albums (release date on or before now) and albums with an unknown/default
    /// release date are always kept. A <c>null</c> or non-positive (<c>&lt;= 0</c>) limit disables
    /// filtering entirely — "0 = include all", matching README.md's documented semantics for this
    /// field (the setting was previously dead, so this is the first real implementation of that
    /// contract, not a change to existing behavior).
    /// </summary>
    public static IReadOnlyList<TidalAlbumInfo> Apply(
        IReadOnlyList<TidalAlbumInfo> albums,
        int? earlyReleaseLimitDays,
        DateTime utcNow)
    {
        if (albums is null || albums.Count == 0)
        {
            return [];
        }

        if (earlyReleaseLimitDays is not { } limit || limit <= 0)
        {
            return albums;
        }

        DateTime cutoff = utcNow.Date.AddDays(limit);
        return [.. albums.Where(a => a.ReleaseDate <= cutoff)];
    }
}
