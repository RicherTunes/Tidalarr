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
    /// <summary>
    /// Builds the ordered combined → artist-only → album-only fallback tiers for a search via the
    /// canonical Common <see cref="SearchQuerySanitizer"/>. <paramref name="album"/> is null for an
    /// artist-only (RSS/discography) search.
    /// </summary>
    internal static SearchPlan Build(string artist, string? album)
        => SearchQuerySanitizer.BuildPlan(artist, album);
}
