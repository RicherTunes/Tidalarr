using System.Collections.Generic;
using System.Threading.Tasks;
using Lidarr.Plugin.Common.Services.Intelligence;
using Lidarr.Plugin.Common.TestKit.Compliance;
using Tidalarr.Core.Models;
using Tidalarr.Integration.LidarrNative;

namespace Tidalarr.Tests.Unit.LidarrNative;

/// <summary>
/// Tidalarr's adoption of the cross-plugin <c>search-term-provenance</c> compliance axis: proves the
/// indexer only ever issues API search terms that came from <see cref="SearchQuerySanitizer.BuildPlan"/>
/// (via the request generator's real plan path, <see cref="TidalSearchPlan.Build"/>) and that the first
/// query issued is the combined tier's first variant.
///
/// <para><see cref="CaptureIssuedQueriesAsync"/> drives the SAME execution path the production
/// <c>FetchReleases</c> uses — <see cref="TidalAlbumSearch.ExecuteAsync"/> (Common's SearchPlanExecutor
/// under Tidal's stop policy + "Tidal search" label) — against a capturing transport that records every
/// query and returns no albums, so the executor walks every tier and we observe the full issue order.</para>
/// </summary>
public sealed class TidalSearchTermProvenanceTests : SearchTermProvenanceComplianceTestBase
{
    protected override SearchPlan BuildPlanViaPlugin(string artist, string album) =>
        TidalSearchPlan.Build(artist, album);

    protected override async Task<IReadOnlyList<string>> CaptureIssuedQueriesAsync(string artist, string album)
    {
        var issued = new List<string>();
        var plan = TidalSearchPlan.Build(artist, album);

        _ = await TidalAlbumSearch.ExecuteAsync(
            plan.Tiers,
            (q, _) =>
            {
                issued.Add(q);
                return Task.FromResult(new TidalSearchResults([], [], [], 0, false));
            });

        return issued;
    }
}
