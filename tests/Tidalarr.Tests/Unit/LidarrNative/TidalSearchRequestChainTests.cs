using System.Collections.Generic;
using Lidarr.Plugin.Common.TestKit.Compliance;
using Tidalarr.Integration.LidarrNative;

namespace Tidalarr.Tests.Unit.LidarrNative;

/// <summary>
/// Tidalarr's adoption of the cross-plugin <c>search-request-chain</c> compliance axis
/// (<see cref="SearchRequestChainComplianceTestBase"/>). Drives the generator's REAL plan→placeholder-URL
/// mapping (<see cref="TidalLidarrRequestGenerator.BuildSearchPlaceholderUrlsForTest"/>, the host-free
/// core of <c>GetSearchRequests</c>) and decodes every URL through <c>PlaceholderSearchUri</c>. Proves
/// the request chain is complete (every BuildPlan variant incl. the full artist-only fallback tier — no
/// Take(N) truncation), placeholder-encoded, combined-first, and sanitized for special characters.
///
/// <para>It is intentionally host-free (no <c>NzbDrone.*</c> types) so it runs inside the
/// <c>ExcludeHostBridge=true</c> CI test build — i.e. the guard actually gates, rather than drifting
/// outside the merge gate.</para>
/// </summary>
public sealed class TidalSearchRequestChainTests : SearchRequestChainComplianceTestBase
{
    protected override string PlaceholderScheme => TidalSearchPlan.SearchScheme;

    // Tidal emits EVERY BuildPlan variant in exact plan order (no cap, no reorder) — opt into
    // the F03 exact-sequence guard so duplicates and post-position-0 reorderings are caught.
    protected override bool RequiresExactPlanSequence => true;

    protected override IReadOnlyList<string> GetSearchRequestUrls(string artist, string album)
        => TidalSearchPlan.BuildSearchPlaceholderUrls(artist, album);
}
