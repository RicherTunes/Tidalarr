using System.Collections.Generic;
using System.Linq;
using Lidarr.Plugin.Common.HostBridge;
using Lidarr.Plugin.Common.Services.Intelligence;

namespace Tidalarr.Integration.LidarrNative;

/// <summary>
/// The Tidal request generator's single plan-construction entry point.
/// <see cref="TidalLidarrRequestGenerator.GetSearchRequests(NzbDrone.Core.IndexerSearch.Definitions.AlbumSearchCriteria)"/>
/// routes through here, so the parity (<c>SearchQuerySanitizerParityTestBase</c>) and provenance
/// (<c>SearchTermProvenanceComplianceTestBase</c>) suites pin the path the live host actually drives —
/// not a parallel second call to <see cref="SearchQuerySanitizer.BuildPlan(string, string, SanitizerOptions)"/>.
///
/// <para>This is intentionally a standalone, Lidarr.Core-free helper (it does NOT derive from a host
/// type) so the hermetic test gate can reference it without the full Lidarr assemblies.</para>
/// </summary>
internal static class TidalSearchPlan
{
    /// <summary>The PlaceholderSearchUri scheme every Tidal search request is encoded under.</summary>
    internal const string SearchScheme = "tidal";

    /// <summary>
    /// Builds the ordered combined → artist-only → album-only fallback tiers for a search via the
    /// canonical Common <see cref="SearchQuerySanitizer"/>. <paramref name="album"/> is null for an
    /// artist-only (RSS/discography) search.
    /// </summary>
    internal static SearchPlan Build(string artist, string? album)
        => SearchQuerySanitizer.BuildPlan(artist, album);

    /// <summary>
    /// Host-free view of the placeholder search URLs the request generator issues for (artist, album), in
    /// chain order — the same <see cref="Build"/> plan and same <see cref="PlaceholderSearchUri"/> encoding
    /// the generator's <c>BuildSearchRequest</c> uses, without the host <c>IndexerRequest</c> wrapping. Lets
    /// the cross-plugin search-request-chain compliance guard drive the real chain inside the hermetic
    /// (Lidarr.Core-free) test gate.
    /// </summary>
    internal static IReadOnlyList<string> BuildSearchPlaceholderUrls(string artist, string? album)
        => Build(artist, album).Tiers
            .SelectMany(tier => tier)
            .Select(term => PlaceholderSearchUri.Build(SearchScheme, term))
            .ToList();
}
