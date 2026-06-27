using System.Collections.Generic;
using System.Linq;
using Lidarr.Plugin.Common.TestKit.Compliance;
using NLog;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Music;
using Tidalarr.Integration.LidarrNative;

namespace Tidalarr.Tests.Unit.LidarrNative;

/// <summary>
/// Tidalarr's adoption of the cross-plugin <c>search-request-chain</c> compliance axis
/// (<see cref="SearchRequestChainComplianceTestBase"/>): drives the REAL
/// <see cref="TidalLidarrRequestGenerator.GetSearchRequests(AlbumSearchCriteria)"/> and decodes every
/// emitted request through <c>PlaceholderSearchUri</c>. Proves the request chain is complete (every
/// BuildPlan variant incl. the full artist-only fallback tier — no Take(N) truncation), placeholder-
/// encoded, combined-first, and sanitized for special characters.
/// </summary>
public sealed class TidalSearchRequestChainTests : SearchRequestChainComplianceTestBase
{
    protected override string PlaceholderScheme => "tidal";

    protected override IReadOnlyList<string> GetSearchRequestUrls(string artist, string album)
    {
        var generator = new TidalLidarrRequestGenerator(
            new TidalLidarrIndexerSettings(),
            LogManager.GetCurrentClassLogger());

        // The host derives ArtistQuery from Artist.Name and AlbumQuery from the criteria's AlbumTitle
        // property (NOT Albums[0].Title), so set both fields the request generator actually reads.
        var chain = generator.GetSearchRequests(new AlbumSearchCriteria
        {
            Artist = new Artist { Name = artist },
            AlbumTitle = album,
            Albums = new List<Album> { new Album { Title = album } },
        });

        return chain.GetAllTiers()
            .SelectMany(tier => tier.Select(request => request.Url.FullUri))
            .ToList();
    }
}
